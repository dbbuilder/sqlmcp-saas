using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data.Metadata;
using DatabaseAutomationPlatform.Infrastructure.Data.Parameters;
using DatabaseAutomationPlatform.Infrastructure.Data.Results;
using DatabaseAutomationPlatform.Infrastructure.Data.Security;
using DatabaseAutomationPlatform.Infrastructure.Logging;

namespace DatabaseAutomationPlatform.Infrastructure.Data
{
    /// <summary>
    /// Secure implementation of stored procedure executor with comprehensive audit logging
    /// </summary>
    public class StoredProcedureExecutor : IStoredProcedureExecutor
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<StoredProcedureExecutor> _logger;
        private readonly ISecurityLogger _securityLogger;
        private readonly ParameterSanitizer _parameterSanitizer;
        private readonly IMemoryCache _metadataCache;
        private readonly DatabaseOptions _options;
        
        // Polly policies for resilience
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly IAsyncPolicy _combinedPolicy;
        // Constants
        private const int DefaultTimeoutSeconds = 30;
        private const string MetadataCacheKeyPrefix = "sp_metadata_";
        
        public StoredProcedureExecutor(
            IDbConnectionFactory connectionFactory,
            ILogger<StoredProcedureExecutor> logger,
            ISecurityLogger securityLogger,
            IOptions<DatabaseOptions> options,
            IMemoryCache memoryCache)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _metadataCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _parameterSanitizer = new ParameterSanitizer(logger);

            // Configure retry policy with exponential backoff and jitter
            _retryPolicy = Policy
                .Handle<SqlException>(IsTransientError)
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => ex.Message.Contains("connection"))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        var procedureName = context.Values.ContainsKey("ProcedureName") ? context.Values["ProcedureName"] : "Unknown";
                        _logger.LogWarning(exception,
                            "Retry {RetryCount} for procedure {ProcedureName} after {Delay}ms. Error: {ErrorMessage}",
                            retryCount, procedureName, timeSpan.TotalMilliseconds, exception.Message);
                    });

            // Configure circuit breaker
            _circuitBreakerPolicy = Policy
                .Handle<SqlException>(IsTransientError)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(exception,
                            "Circuit breaker opened for {Duration}s due to repeated failures",
                            duration.TotalSeconds);
                        _securityLogger.LogSecurityEvent("CircuitBreakerOpened", 
                            new { Duration = duration, Exception = exception.Message });
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                        _securityLogger.LogSecurityEvent("CircuitBreakerReset", null);
                    });

            // Combine policies: circuit breaker wraps retry
            _combinedPolicy = Policy.WrapAsync(_circuitBreakerPolicy, _retryPolicy);
        }
        /// <inheritdoc/>
        public async Task<StoredProcedureResult<int>> ExecuteNonQueryAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));

            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();
            var paramList = parameters?.ToList() ?? new List<StoredProcedureParameter>();
            var retryCount = 0;

            try
            {
                // Log execution start
                _logger.LogInformation(
                    "Starting ExecuteNonQuery for {ProcedureName} with {ParameterCount} parameters. ExecutionId: {ExecutionId}",
                    procedureName, paramList.Count, executionId);

                // Validate and sanitize parameters
                var sanitizationResult = _parameterSanitizer.ValidateParameters(procedureName, paramList);
                if (!sanitizationResult.IsValid)
                {
                    var error = $"Parameter validation failed: {string.Join(", ", sanitizationResult.Errors)}";
                    _securityLogger.LogSecurityEvent("ParameterValidationFailed", 
                        new { ProcedureName = procedureName, Errors = sanitizationResult.Errors, ExecutionId = executionId });
                    
                    return StoredProcedureResult<int>.Failure(error, procedureName, stopwatch.ElapsedMilliseconds);
                }
                // Log warnings if any
                if (sanitizationResult.Warnings.Any())
                {
                    _logger.LogWarning(
                        "Parameter validation warnings for {ProcedureName}: {Warnings}",
                        procedureName, string.Join(", ", sanitizationResult.Warnings));
                }

                // Execute with retry and circuit breaker
                var policyContext = new Context
                {
                    { "ProcedureName", procedureName },
                    { "ExecutionId", executionId }
                };

                var result = await _combinedPolicy.ExecuteAsync(async (context, ct) =>
                {
                    retryCount = context.ContainsKey("RetryCount") ? (int)context["RetryCount"] : 0;
                    context["RetryCount"] = retryCount + 1;

                    await using var connection = await _connectionFactory.CreateConnectionAsync();
                    await using var command = CreateCommand(connection, procedureName, paramList, timeoutSeconds);

                    // Log the command being executed (without sensitive data)
                    LogCommandExecution(command, procedureName, executionId);

                    var rowsAffected = await command.ExecuteNonQueryAsync(ct);

                    // Extract output parameters and return value
                    var outputParams = ExtractOutputParameters(command);
                    var returnValue = ExtractReturnValue(command);

                    return new
                    {
                        RowsAffected = rowsAffected,
                        OutputParameters = outputParams,
                        ReturnValue = returnValue
                    };
                }, policyContext, cancellationToken);
                stopwatch.Stop();

                // Log successful execution
                _logger.LogInformation(
                    "Successfully executed {ProcedureName}. Rows affected: {RowsAffected}, Duration: {Duration}ms, ExecutionId: {ExecutionId}",
                    procedureName, result.RowsAffected, stopwatch.ElapsedMilliseconds, executionId);

                // Audit log for successful execution
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureExecuted",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = true,
                    RowsAffected = result.RowsAffected,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount,
                    Parameters = GetParametersForAudit(paramList)
                });

                return StoredProcedureResult<int>.Success(
                    data: result.RowsAffected,
                    procedureName: procedureName,
                    executionTimeMs: stopwatch.ElapsedMilliseconds,
                    rowsAffected: result.RowsAffected,
                    outputParameters: result.OutputParameters,
                    returnValue: result.ReturnValue,
                    retryCount: retryCount);
            }
            catch (CircuitBreakerOpenException ex)
            {
                stopwatch.Stop();
                var error = "Circuit breaker is open. Too many recent failures.";
                _logger.LogError(ex, error);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureCircuitBreakerOpen",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = error,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                });

                return StoredProcedureResult<int>.Failure(error, procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }            catch (SqlException ex)
            {
                stopwatch.Stop();
                var error = $"SQL error executing procedure: {ex.Message}";
                _logger.LogError(ex, "SQL error executing {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureSqlError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = GetSafeErrorMessage(ex),
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount
                });

                return StoredProcedureResult<int>.Failure(GetSafeErrorMessage(ex), procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error executing {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = "An unexpected error occurred",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount
                });

                return StoredProcedureResult<int>.Failure("An unexpected error occurred", procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }
        }
        /// <inheritdoc/>
        public async Task<StoredProcedureResult<T>> ExecuteScalarAsync<T>(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));

            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();
            var paramList = parameters?.ToList() ?? new List<StoredProcedureParameter>();
            var retryCount = 0;

            try
            {
                // Log execution start
                _logger.LogInformation(
                    "Starting ExecuteScalar<{Type}> for {ProcedureName}. ExecutionId: {ExecutionId}",
                    typeof(T).Name, procedureName, executionId);

                // Validate and sanitize parameters
                var sanitizationResult = _parameterSanitizer.ValidateParameters(procedureName, paramList);
                if (!sanitizationResult.IsValid)
                {
                    var error = $"Parameter validation failed: {string.Join(", ", sanitizationResult.Errors)}";
                    _securityLogger.LogSecurityEvent("ParameterValidationFailed", 
                        new { ProcedureName = procedureName, Errors = sanitizationResult.Errors, ExecutionId = executionId });
                    
                    return StoredProcedureResult<T>.Failure(error, procedureName, stopwatch.ElapsedMilliseconds);
                }
                // Execute with retry and circuit breaker
                var policyContext = new Context
                {
                    { "ProcedureName", procedureName },
                    { "ExecutionId", executionId }
                };

                var result = await _combinedPolicy.ExecuteAsync(async (context, ct) =>
                {
                    retryCount = context.ContainsKey("RetryCount") ? (int)context["RetryCount"] : 0;
                    context["RetryCount"] = retryCount + 1;

                    await using var connection = await _connectionFactory.CreateConnectionAsync();
                    await using var command = CreateCommand(connection, procedureName, paramList, timeoutSeconds);

                    // Log the command being executed
                    LogCommandExecution(command, procedureName, executionId);

                    var scalarResult = await command.ExecuteScalarAsync(ct);

                    // Extract output parameters and return value
                    var outputParams = ExtractOutputParameters(command);
                    var returnValue = ExtractReturnValue(command);

                    // Convert the result to the requested type
                    T? typedResult = default(T);
                    if (scalarResult != null && scalarResult != DBNull.Value)
                    {
                        try
                        {
                            typedResult = (T)Convert.ChangeType(scalarResult, typeof(T));
                        }
                        catch (InvalidCastException)
                        {
                            // Try direct cast as fallback
                            typedResult = (T)scalarResult;
                        }
                    }

                    return new
                    {
                        Data = typedResult,
                        OutputParameters = outputParams,
                        ReturnValue = returnValue
                    };
                }, policyContext, cancellationToken);
                stopwatch.Stop();

                // Log successful execution
                _logger.LogInformation(
                    "Successfully executed scalar {ProcedureName}. Duration: {Duration}ms, ExecutionId: {ExecutionId}",
                    procedureName, stopwatch.ElapsedMilliseconds, executionId);

                // Audit log
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureScalarExecuted",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = true,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount,
                    Parameters = GetParametersForAudit(paramList)
                });

                return StoredProcedureResult<T>.Success(
                    data: result.Data,
                    procedureName: procedureName,
                    executionTimeMs: stopwatch.ElapsedMilliseconds,
                    outputParameters: result.OutputParameters,
                    returnValue: result.ReturnValue,
                    retryCount: retryCount);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var safeError = GetSafeErrorMessage(ex);
                _logger.LogError(ex, "Error executing scalar {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureScalarError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = safeError,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount
                });

                return StoredProcedureResult<T>.Failure(safeError, procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }
        }
        /// <inheritdoc/>
        public async Task<StoredProcedureResult<IEnumerable<T>>> ExecuteReaderAsync<T>(
            string procedureName,
            Func<IDataReader, T> mapper,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();
            var paramList = parameters?.ToList() ?? new List<StoredProcedureParameter>();
            var retryCount = 0;

            try
            {
                _logger.LogInformation(
                    "Starting ExecuteReader<{Type}> for {ProcedureName}. ExecutionId: {ExecutionId}",
                    typeof(T).Name, procedureName, executionId);

                // Validate and sanitize parameters
                var sanitizationResult = _parameterSanitizer.ValidateParameters(procedureName, paramList);
                if (!sanitizationResult.IsValid)
                {
                    var error = $"Parameter validation failed: {string.Join(", ", sanitizationResult.Errors)}";
                    _securityLogger.LogSecurityEvent("ParameterValidationFailed", 
                        new { ProcedureName = procedureName, Errors = sanitizationResult.Errors, ExecutionId = executionId });
                    
                    return StoredProcedureResult<IEnumerable<T>>.Failure(error, procedureName, stopwatch.ElapsedMilliseconds);
                }
                // Execute with retry and circuit breaker
                var policyContext = new Context
                {
                    { "ProcedureName", procedureName },
                    { "ExecutionId", executionId }
                };

                var result = await _combinedPolicy.ExecuteAsync(async (context, ct) =>
                {
                    retryCount = context.ContainsKey("RetryCount") ? (int)context["RetryCount"] : 0;
                    context["RetryCount"] = retryCount + 1;

                    await using var connection = await _connectionFactory.CreateConnectionAsync();
                    await using var command = CreateCommand(connection, procedureName, paramList, timeoutSeconds);

                    // Log the command being executed
                    LogCommandExecution(command, procedureName, executionId);

                    var results = new List<T>();
                    await using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            results.Add(mapper(reader));
                        }
                    }

                    // Extract output parameters after reader is closed
                    var outputParams = ExtractOutputParameters(command);
                    var returnValue = ExtractReturnValue(command);

                    return new
                    {
                        Data = results,
                        OutputParameters = outputParams,
                        ReturnValue = returnValue,
                        RowCount = results.Count
                    };
                }, policyContext, cancellationToken);
                stopwatch.Stop();

                // Log successful execution
                _logger.LogInformation(
                    "Successfully executed reader {ProcedureName}. Rows: {RowCount}, Duration: {Duration}ms, ExecutionId: {ExecutionId}",
                    procedureName, result.RowCount, stopwatch.ElapsedMilliseconds, executionId);

                // Audit log
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureReaderExecuted",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = true,
                    RowsAffected = result.RowCount,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount,
                    Parameters = GetParametersForAudit(paramList)
                });

                return StoredProcedureResult<IEnumerable<T>>.Success(
                    data: result.Data,
                    procedureName: procedureName,
                    executionTimeMs: stopwatch.ElapsedMilliseconds,
                    rowsAffected: result.RowCount,
                    outputParameters: result.OutputParameters,
                    returnValue: result.ReturnValue,
                    retryCount: retryCount);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var safeError = GetSafeErrorMessage(ex);
                _logger.LogError(ex, "Error executing reader {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureReaderError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = safeError,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount
                });

                return StoredProcedureResult<IEnumerable<T>>.Failure(safeError, procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }
        }
        /// <inheritdoc/>
        public async Task<StoredProcedureResult<DataSet>> ExecuteDataSetAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));

            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();
            var paramList = parameters?.ToList() ?? new List<StoredProcedureParameter>();
            var retryCount = 0;

            try
            {
                _logger.LogInformation(
                    "Starting ExecuteDataSet for {ProcedureName}. ExecutionId: {ExecutionId}",
                    procedureName, executionId);

                // Validate and sanitize parameters
                var sanitizationResult = _parameterSanitizer.ValidateParameters(procedureName, paramList);
                if (!sanitizationResult.IsValid)
                {
                    var error = $"Parameter validation failed: {string.Join(", ", sanitizationResult.Errors)}";
                    _securityLogger.LogSecurityEvent("ParameterValidationFailed", 
                        new { ProcedureName = procedureName, Errors = sanitizationResult.Errors, ExecutionId = executionId });
                    
                    return StoredProcedureResult<DataSet>.Failure(error, procedureName, stopwatch.ElapsedMilliseconds);
                }
                // Execute with retry and circuit breaker
                var policyContext = new Context
                {
                    { "ProcedureName", procedureName },
                    { "ExecutionId", executionId }
                };

                var result = await _combinedPolicy.ExecuteAsync(async (context, ct) =>
                {
                    retryCount = context.ContainsKey("RetryCount") ? (int)context["RetryCount"] : 0;
                    context["RetryCount"] = retryCount + 1;

                    await using var connection = await _connectionFactory.CreateConnectionAsync();
                    await using var command = CreateCommand(connection, procedureName, paramList, timeoutSeconds);

                    // Log the command being executed
                    LogCommandExecution(command, procedureName, executionId);

                    var dataSet = new DataSet();
                    using (var adapter = new SqlDataAdapter((SqlCommand)command))
                    {
                        adapter.Fill(dataSet);
                    }

                    // Extract output parameters after data adapter is done
                    var outputParams = ExtractOutputParameters(command);
                    var returnValue = ExtractReturnValue(command);

                    return new
                    {
                        Data = dataSet,
                        OutputParameters = outputParams,
                        ReturnValue = returnValue,
                        TableCount = dataSet.Tables.Count,
                        TotalRows = dataSet.Tables.Cast<DataTable>().Sum(t => t.Rows.Count)
                    };
                }, policyContext, cancellationToken);
                stopwatch.Stop();

                // Log successful execution
                _logger.LogInformation(
                    "Successfully executed dataset {ProcedureName}. Tables: {TableCount}, Total rows: {TotalRows}, Duration: {Duration}ms, ExecutionId: {ExecutionId}",
                    procedureName, result.TableCount, result.TotalRows, stopwatch.ElapsedMilliseconds, executionId);

                // Audit log
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureDataSetExecuted",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = true,
                    RowsAffected = result.TotalRows,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount,
                    Parameters = GetParametersForAudit(paramList),
                    AdditionalData = new { TableCount = result.TableCount }
                });

                return StoredProcedureResult<DataSet>.Success(
                    data: result.Data,
                    procedureName: procedureName,
                    executionTimeMs: stopwatch.ElapsedMilliseconds,
                    rowsAffected: result.TotalRows,
                    outputParameters: result.OutputParameters,
                    returnValue: result.ReturnValue,
                    retryCount: retryCount);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var safeError = GetSafeErrorMessage(ex);
                _logger.LogError(ex, "Error executing dataset {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureDataSetError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = safeError,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RetryCount = retryCount
                });

                return StoredProcedureResult<DataSet>.Failure(safeError, procedureName, stopwatch.ElapsedMilliseconds, retryCount);
            }
        }
        /// <inheritdoc/>
        public async Task<StoredProcedureResult<T>> ExecuteInTransactionAsync<T>(
            string procedureName,
            Func<IDbTransaction, Task<T>> executeAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));
            if (executeAction == null)
                throw new ArgumentNullException(nameof(executeAction));

            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation(
                    "Starting transactional execution for {ProcedureName}. IsolationLevel: {IsolationLevel}, ExecutionId: {ExecutionId}",
                    procedureName, isolationLevel, executionId);

                await using var connection = await _connectionFactory.CreateConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

                try
                {
                    var result = await executeAction(transaction);
                    await transaction.CommitAsync(cancellationToken);

                    stopwatch.Stop();
                    
                    _logger.LogInformation(
                        "Successfully committed transaction for {ProcedureName}. Duration: {Duration}ms, ExecutionId: {ExecutionId}",
                        procedureName, stopwatch.ElapsedMilliseconds, executionId);
                    await LogAuditEventAsync(new AuditEvent
                    {
                        EventType = "StoredProcedureTransactionCommitted",
                        ProcedureName = procedureName,
                        ExecutionId = executionId,
                        Success = true,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        AdditionalData = new { IsolationLevel = isolationLevel.ToString() }
                    });

                    return StoredProcedureResult<T>.Success(
                        data: result,
                        procedureName: procedureName,
                        executionTimeMs: stopwatch.ElapsedMilliseconds);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var safeError = GetSafeErrorMessage(ex);
                _logger.LogError(ex, "Error in transactional execution for {ProcedureName}", procedureName);
                
                await LogAuditEventAsync(new AuditEvent
                {
                    EventType = "StoredProcedureTransactionError",
                    ProcedureName = procedureName,
                    ExecutionId = executionId,
                    Success = false,
                    ErrorMessage = safeError,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    AdditionalData = new { IsolationLevel = isolationLevel.ToString() }
                });

                return StoredProcedureResult<T>.Failure(safeError, procedureName, stopwatch.ElapsedMilliseconds);
            }
        }
        /// <inheritdoc/>
        public async Task<bool> ValidateProcedureAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));

            try
            {
                // Check metadata cache first
                var cacheKey = $"{MetadataCacheKeyPrefix}{procedureName}";
                if (_metadataCache.TryGetValue<StoredProcedureMetadata>(cacheKey, out var cachedMetadata))
                {
                    if (!cachedMetadata.IsCacheExpired())
                    {
                        _logger.LogDebug("Using cached metadata for {ProcedureName}", procedureName);
                        
                        if (parameters != null)
                        {
                            var validationResult = cachedMetadata.ValidateParameters(parameters);
                            if (!validationResult.IsValid)
                            {
                                _logger.LogWarning(
                                    "Parameter validation failed for {ProcedureName}: {Errors}",
                                    procedureName, string.Join(", ", validationResult.Errors));
                                return false;
                            }
                        }
                        
                        return true;
                    }
                }
                // Validate by checking if procedure exists in database
                await using var connection = await _connectionFactory.CreateConnectionAsync();
                await using var command = connection.CreateCommand();
                
                command.CommandText = @"
                    SELECT 1 
                    FROM sys.procedures p
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                    WHERE s.name + '.' + p.name = @ProcedureName
                       OR p.name = @ProcedureName";
                
                command.Parameters.Add(new SqlParameter("@ProcedureName", procedureName));
                
                var exists = await command.ExecuteScalarAsync(cancellationToken) != null;
                
                if (!exists)
                {
                    _logger.LogWarning("Stored procedure {ProcedureName} does not exist", procedureName);
                    _securityLogger.LogSecurityEvent("InvalidStoredProcedureAccess", 
                        new { ProcedureName = procedureName });
                }
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating stored procedure {ProcedureName}", procedureName);
                return false;
            }
        }
        #region Helper Methods

        /// <summary>
        /// Creates a command for executing a stored procedure
        /// </summary>
        private DbCommand CreateCommand(
            IDbConnection connection, 
            string procedureName, 
            IEnumerable<StoredProcedureParameter> parameters,
            int? timeoutSeconds)
        {
            if (!(connection is SqlConnection sqlConnection))
                throw new InvalidOperationException("Connection must be a SqlConnection");

            var command = sqlConnection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = timeoutSeconds ?? _options.CommandTimeout ?? DefaultTimeoutSeconds;

            // Add parameters
            foreach (var parameter in parameters)
            {
                parameter.ApplyToCommand(command);
            }

            // Add return value parameter if not already present
            if (!command.Parameters.Cast<SqlParameter>().Any(p => p.Direction == ParameterDirection.ReturnValue))
            {
                var returnParam = StoredProcedureParameter.ReturnValue();
                returnParam.ApplyToCommand(command);
            }

            return command;
        }
        /// <summary>
        /// Logs command execution details (without sensitive data)
        /// </summary>
        private void LogCommandExecution(DbCommand command, string procedureName, string executionId)
        {
            var paramInfo = command.Parameters
                .Cast<SqlParameter>()
                .Select(p => new
                {
                    Name = p.ParameterName,
                    Type = p.SqlDbType.ToString(),
                    Direction = p.Direction.ToString(),
                    Size = p.Size
                })
                .ToList();

            _logger.LogDebug(
                "Executing command. Procedure: {ProcedureName}, Timeout: {Timeout}s, Parameters: {Parameters}, ExecutionId: {ExecutionId}",
                procedureName, command.CommandTimeout, paramInfo, executionId);

            // Log the actual SQL being executed for debugging (in development only)
            if (_options.EnableSqlLogging)
            {
                var sql = $"EXEC {procedureName} " + string.Join(", ", 
                    command.Parameters.Cast<SqlParameter>()
                        .Where(p => p.Direction == ParameterDirection.Input || p.Direction == ParameterDirection.InputOutput)
                        .Select(p => $"{p.ParameterName} = @{p.ParameterName.TrimStart('@')}"));
                
                _logger.LogDebug("SQL Command: {SqlCommand}", sql);
                System.Console.WriteLine($"-- Dynamic SQL:\n{sql}");
            }
        }
        /// <summary>
        /// Extracts output parameters from command
        /// </summary>
        private Dictionary<string, object?> ExtractOutputParameters(DbCommand command)
        {
            var outputParams = new Dictionary<string, object?>();
            
            foreach (SqlParameter param in command.Parameters)
            {
                if (param.Direction == ParameterDirection.Output || 
                    param.Direction == ParameterDirection.InputOutput)
                {
                    outputParams[param.ParameterName] = param.Value == DBNull.Value ? null : param.Value;
                }
            }
            
            return outputParams;
        }

        /// <summary>
        /// Extracts return value from command
        /// </summary>
        private int? ExtractReturnValue(DbCommand command)
        {
            var returnParam = command.Parameters
                .Cast<SqlParameter>()
                .FirstOrDefault(p => p.Direction == ParameterDirection.ReturnValue);
            
            if (returnParam?.Value != null && returnParam.Value != DBNull.Value)
            {
                return Convert.ToInt32(returnParam.Value);
            }
            
            return null;
        }
        /// <summary>
        /// Gets parameters for audit logging (masks sensitive values)
        /// </summary>
        private Dictionary<string, string> GetParametersForAudit(IEnumerable<StoredProcedureParameter> parameters)
        {
            return parameters.ToDictionary(
                p => p.Name,
                p => p.ToLogString());
        }

        /// <summary>
        /// Gets safe error message that doesn't expose internal details
        /// </summary>
        private string GetSafeErrorMessage(Exception ex)
        {
            return ex switch
            {
                SqlException sqlEx when IsTransientError(sqlEx) => "A temporary database error occurred. Please try again.",
                SqlException sqlEx when sqlEx.Number == 2812 => "The requested stored procedure does not exist.",
                SqlException sqlEx when sqlEx.Number == 201 => "Invalid parameters provided to stored procedure.",
                TimeoutException => "The operation timed out. Please try again or contact support if the issue persists.",
                InvalidCastException => "Data type mismatch in result processing.",
                CircuitBreakerOpenException => "Service temporarily unavailable due to repeated failures.",
                _ => "An error occurred while executing the database operation."
            };
        }

        /// <summary>
        /// Determines if a SQL exception is transient and should be retried
        /// </summary>
        private bool IsTransientError(SqlException ex)
        {
            // List of SQL error numbers that are considered transient
            int[] transientErrors = new[] 
            {
                1205,   // Deadlock
                1222,   // Lock timeout
                49918,  // Cannot process request. Not enough resources
                49919,  // Cannot process create or update request. Too many operations
                49920,  // Cannot process request. Too many operations
                4060,   // Cannot open database
                40143,  // Service has encountered an error
                40197,  // Service has encountered an error processing request
                40501,  // Service is busy
                40613,  // Database is not currently available
                64      // A connection was successfully established but then an error occurred
            };
            
            return transientErrors.Contains(ex.Number);
        }
        /// <summary>
        /// Logs audit event asynchronously
        /// </summary>
        private async Task LogAuditEventAsync(AuditEvent auditEvent)
        {
            try
            {
                // Log to security logger
                _securityLogger.LogSecurityEvent(auditEvent.EventType, auditEvent);
                
                // Log to structured logging
                _logger.LogInformation(
                    "Audit: {EventType} for {ProcedureName}. Success: {Success}, ExecutionId: {ExecutionId}",
                    auditEvent.EventType, auditEvent.ProcedureName, auditEvent.Success, auditEvent.ExecutionId);
                
                // In a real implementation, this would also write to an audit table
                // await _auditRepository.LogEventAsync(auditEvent);
                
                await Task.CompletedTask; // Placeholder for async audit operations
            }
            catch (Exception ex)
            {
                // Audit logging should not fail the main operation
                _logger.LogError(ex, "Failed to log audit event for {ProcedureName}", auditEvent.ProcedureName);
            }
        }

        #endregion

        /// <summary>
        /// Internal audit event class
        /// </summary>
        private class AuditEvent
        {
            public string EventType { get; set; } = string.Empty;
            public string ProcedureName { get; set; } = string.Empty;
            public string ExecutionId { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public long ExecutionTimeMs { get; set; }
            public int? RowsAffected { get; set; }
            public int RetryCount { get; set; }
            public Dictionary<string, string>? Parameters { get; set; }
            public object? AdditionalData { get; set; }
        }
    }
}