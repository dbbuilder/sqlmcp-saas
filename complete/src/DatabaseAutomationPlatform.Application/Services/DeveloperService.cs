using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Application.Services;

/// <summary>
/// Service implementation for SQL Developer operations
/// </summary>
public class DeveloperService : IDeveloperService
{
    private readonly IStoredProcedureExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeveloperService> _logger;

    public DeveloperService(
        IStoredProcedureExecutor executor,
        IUnitOfWork unitOfWork,
        ILogger<DeveloperService> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryResult> ExecuteQueryAsync(string database, string query, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));
        if (timeoutSeconds < 1 || timeoutSeconds > 300) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be between 1 and 300 seconds");

        _logger.LogInformation("Executing query on database {Database}", database);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "ExecuteQuery",
            EntityType = "Database",
            EntityId = database,
            AdditionalData = $"Query: {query.Substring(0, Math.Min(query.Length, 500))}"
        };

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@Query", query),
                new SqlParameter("@Timeout", timeoutSeconds),
                new SqlParameter("@ExecutionTimeMs", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
            };

            var result = await _executor.ExecuteAsync("sp_ExecuteQuery", parameters, cancellationToken);
            var executionTime = Convert.ToInt64(parameters[3].Value);

            var columns = result.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            var rows = result.Rows.Cast<DataRow>().Select(r => r.ItemArray).ToArray();

            auditEvent.Success = true;
            auditEvent.Duration = (int)executionTime;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            _logger.LogInformation("Query executed successfully. Rows: {RowCount}, Time: {ExecutionTime}ms", 
                rows.Length, executionTime);

            return new QueryResult
            {
                Columns = columns,
                Rows = rows!,
                RowCount = rows.Length,
                ExecutionTimeMs = executionTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query on database {Database}", database);
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            throw;
        }
    }

    public async Task<CommandResult> ExecuteCommandAsync(string database, string command, bool useTransaction = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Command is required", nameof(command));

        _logger.LogInformation("Executing command on database {Database} with transaction: {UseTransaction}", database, useTransaction);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "ExecuteCommand",
            EntityType = "Database",
            EntityId = database,
            AdditionalData = $"Command: {command.Substring(0, Math.Min(command.Length, 500))}"
        };

        try
        {
            if (useTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
            }

            var parameters = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@Command", command),
                new SqlParameter("@UseTransaction", useTransaction),
                new SqlParameter("@AffectedRows", SqlDbType.Int) { Direction = ParameterDirection.Output },
                new SqlParameter("@ExecutionTimeMs", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
            };

            await _executor.ExecuteNonQueryAsync("sp_ExecuteCommand", parameters, cancellationToken);
            
            var affectedRows = Convert.ToInt32(parameters[3].Value);
            var executionTime = Convert.ToInt64(parameters[4].Value);

            if (useTransaction)
            {
                await _unitOfWork.CommitAsync(cancellationToken);
            }

            auditEvent.Success = true;
            auditEvent.Duration = (int)executionTime;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            _logger.LogInformation("Command executed successfully. Affected rows: {AffectedRows}, Time: {ExecutionTime}ms", 
                affectedRows, executionTime);

            return new CommandResult
            {
                AffectedRows = affectedRows,
                Success = true,
                ExecutionTimeMs = executionTime,
                Message = $"Command executed successfully. {affectedRows} row(s) affected."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command on database {Database}", database);
            
            if (useTransaction && _unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
            }

            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            throw;
        }
    }

    public async Task<SqlGenerationResult> GenerateSqlAsync(string database, string description, string context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required", nameof(description));

        _logger.LogInformation("Generating SQL for database {Database}: {Description}", database, description);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Description", description),
            new SqlParameter("@Context", context ?? string.Empty)
        };

        var result = await _executor.ExecuteAsync("sp_GenerateSql", parameters, cancellationToken);
        
        if (result.Rows.Count == 0)
        {
            return new SqlGenerationResult
            {
                GeneratedSql = string.Empty,
                Explanation = "Unable to generate SQL for the given description",
                ConfidenceScore = 0,
                Warnings = new[] { "No suitable SQL pattern found" }
            };
        }

        var row = result.Rows[0];
        var warnings = new List<string>();
        
        // Parse warnings if present
        var warningsData = row.Field<string>("Warnings");
        if (!string.IsNullOrEmpty(warningsData))
        {
            warnings.AddRange(warningsData.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        return new SqlGenerationResult
        {
            GeneratedSql = row.Field<string>("GeneratedSql") ?? string.Empty,
            Explanation = row.Field<string>("Explanation") ?? string.Empty,
            ConfidenceScore = Convert.ToDouble(row.Field<decimal>("ConfidenceScore")),
            Warnings = warnings.ToArray()
        };
    }

    public async Task<QueryOptimizationResult> OptimizeQueryAsync(string database, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));

        _logger.LogInformation("Optimizing query for database {Database}", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Query", query)
        };

        var result = await _executor.ExecuteAsync("sp_OptimizeQuery", parameters, cancellationToken);
        
        if (result.Rows.Count == 0)
        {
            return new QueryOptimizationResult
            {
                OptimizedQuery = query,
                Recommendations = new[] { "No optimization opportunities found" },
                EstimatedImprovement = 0,
                OriginalPlan = string.Empty,
                OptimizedPlan = string.Empty
            };
        }

        var recommendations = new List<string>();
        foreach (DataRow row in result.Rows)
        {
            var recommendation = row.Field<string>("Recommendation");
            if (!string.IsNullOrEmpty(recommendation))
            {
                recommendations.Add(recommendation);
            }
        }

        var firstRow = result.Rows[0];
        return new QueryOptimizationResult
        {
            OptimizedQuery = firstRow.Field<string>("OptimizedQuery") ?? query,
            Recommendations = recommendations.ToArray(),
            EstimatedImprovement = Convert.ToDouble(firstRow.Field<decimal>("EstimatedImprovement")),
            OriginalPlan = firstRow.Field<string>("OriginalPlan") ?? string.Empty,
            OptimizedPlan = firstRow.Field<string>("OptimizedPlan") ?? string.Empty
        };
    }

    public async Task<SqlValidationResult> ValidateSqlAsync(string database, string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL is required", nameof(sql));

        _logger.LogInformation("Validating SQL for database {Database}", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Sql", sql)
        };

        var result = await _executor.ExecuteAsync("sp_ValidateSql", parameters, cancellationToken);
        
        var errors = new List<string>();
        var warnings = new List<string>();
        var suggestions = new Dictionary<string, string>();
        var isValid = true;

        foreach (DataRow row in result.Rows)
        {
            isValid = isValid && row.Field<bool>("IsValid");
            
            var error = row.Field<string>("Error");
            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(error);
            }

            var warning = row.Field<string>("Warning");
            if (!string.IsNullOrEmpty(warning))
            {
                warnings.Add(warning);
            }

            var suggestion = row.Field<string>("Suggestion");
            if (!string.IsNullOrEmpty(suggestion))
            {
                // Parse suggestion format: "Category:Suggestion"
                var parts = suggestion.Split(':', 2);
                if (parts.Length == 2)
                {
                    suggestions[parts[0]] = parts[1];
                }
                else
                {
                    suggestions["General"] = suggestion;
                }
            }
        }

        return new SqlValidationResult
        {
            IsValid = isValid,
            Errors = errors.ToArray(),
            Warnings = warnings.ToArray(),
            Suggestions = suggestions
        };
    }
}