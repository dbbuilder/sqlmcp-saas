using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;

namespace DatabaseAutomationPlatform.Infrastructure.Data
{
    /// <summary>
    /// Secure SQL Server connection factory with retry policies and comprehensive logging
    /// </summary>
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly ISecureConnectionStringProvider _connectionStringProvider;
        private readonly ILogger<SqlConnectionFactory> _logger;
        private readonly DatabaseOptions _options;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ISecurityLogger _securityLogger;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // SQL error numbers that indicate transient failures
        private static readonly int[] TransientErrorNumbers = new[]
        {
            49918, // Cannot process request. Not enough resources to process request
            49919, // Cannot process create or update request. Too many operations in progress
            49920, // Cannot process request. Too many operations in progress
            4060,  // Cannot open database requested by the login
            40143, // The service has encountered an error processing your request
            1205,  // Deadlock victim
            40197, // The service has encountered an error processing your request
            40501, // The service is currently busy
            40613, // Database is currently unavailable
            233,   // Connection initialization error
            64,    // A connection was successfully established with the server
            20,    // The instance of SQL Server does not support encryption
            121    // The semaphore timeout period has expired
        };

        /// <summary>
        /// Initializes a new instance of the SqlConnectionFactory class
        /// </summary>
        /// <param name="connectionStringProvider">Provider for secure connection strings</param>
        /// <param name="logger">Logger for general logging</param>
        /// <param name="options">Database configuration options</param>
        /// <param name="securityLogger">Logger for security events</param>
        /// <param name="httpContextAccessor">Optional HTTP context accessor for client IP</param>
        public SqlConnectionFactory(
            ISecureConnectionStringProvider connectionStringProvider,
            ILogger<SqlConnectionFactory> logger,
            IOptions<DatabaseOptions> options,
            ISecurityLogger securityLogger,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _connectionStringProvider = connectionStringProvider ?? 
                throw new ArgumentNullException(nameof(connectionStringProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
            _httpContextAccessor = httpContextAccessor;

            // Validate options
            _options.Validate();

            // Configure retry policy for transient SQL failures with exponential backoff
            _retryPolicy = Policy
                .Handle<SqlException>(IsTransientError)
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    _options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromMilliseconds(
                        _options.RetryDelayMilliseconds * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var exception = outcome.Exception;
                        _logger.LogWarning(
                            exception,
                            "Database connection retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {ErrorMessage}",
                            retryCount,
                            _options.MaxRetryAttempts,
                            timespan.TotalMilliseconds,
                            exception.Message);
                    });
        }

        /// <summary>
        /// Determines if a SQL exception represents a transient error that should be retried
        /// </summary>
        private static bool IsTransientError(SqlException sqlException)
        {
            if (sqlException == null) return false;

            foreach (SqlError error in sqlException.Errors)
            {
                if (TransientErrorNumbers.Contains(error.Number))
                {
                    return true;
                }
            }
            
            // Also check for timeout-related messages
            return sqlException.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   sqlException.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            CancellationToken cancellationToken = default)
        {
            return await CreateConnectionAsync(connectionName, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            string? databaseName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

            using var activity = Diagnostics.ActivitySource.StartActivity("CreateDatabaseConnection");
            activity?.SetTag("connection.name", connectionName);
            activity?.SetTag("database.name", databaseName ?? "default");
            activity?.SetTag("connection.pooling", _options.EnableConnectionPooling);

            SqlConnection? connection = null;
            var clientIp = GetClientIpAddress();

            try
            {
                _logger.LogDebug(
                    "Creating database connection for {ConnectionName} with database {DatabaseName}",
                    connectionName,
                    databaseName ?? "default");

                // Get secure connection string
                var baseConnectionString = await _connectionStringProvider.GetConnectionStringAsync(
                    connectionName, cancellationToken);

                // Build connection string with security and performance options
                var builder = new SqlConnectionStringBuilder(baseConnectionString)
                {
                    ConnectTimeout = _options.ConnectionTimeout,
                    CommandTimeout = _options.CommandTimeout,
                    Encrypt = true,
                    TrustServerCertificate = false,
                    IntegratedSecurity = false,
                    MultipleActiveResultSets = false,
                    MultiSubnetFailover = true,
                    ApplicationName = "DatabaseAutomationPlatform",
                    ApplicationIntent = ApplicationIntent.ReadWrite,
                    Pooling = _options.EnableConnectionPooling,
                    MinPoolSize = _options.MinPoolSize,
                    MaxPoolSize = _options.MaxPoolSize,
                    ConnectRetryCount = 2,
                    ConnectRetryInterval = 10
                };
                
                // Add workstation ID for auditing
                builder.WorkstationID = Environment.MachineName;

                // Override database if specified
                if (!string.IsNullOrWhiteSpace(databaseName))
                {
                    builder.InitialCatalog = databaseName;
                }

                // Create and open connection with retry policy
                connection = new SqlConnection(builder.ConnectionString);
                
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await connection.OpenAsync(cancellationToken);
                    
                    // Validate connection with a simple query
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT @@VERSION";
                    command.CommandTimeout = 5;
                    var version = await command.ExecuteScalarAsync(cancellationToken);
                    
                    _logger.LogDebug("Connected to SQL Server: {Version}", version);
                });

                // Log successful connection (without exposing sensitive data)
                _logger.LogInformation(
                    "Successfully established database connection to {DatabaseName} on {DataSource}",
                    connection.Database,
                    MaskServerName(connection.DataSource));

                // Security audit log for successful connection
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{connection.Database}",
                    Success = true,
                    IpAddress = clientIp,
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["Database"] = connection.Database,
                        ["ServerVersion"] = connection.ServerVersion,
                        ["ConnectionId"] = connection.ClientConnectionId.ToString()
                    }
                });

                activity?.SetStatus(ActivityStatusCode.Ok);
                return connection;
            }
            catch (SqlException sqlEx)
            {
                // Handle SQL-specific errors
                _logger.LogError(sqlEx, 
                    "SQL error occurred while creating connection to {ConnectionName}: Error {ErrorNumber}",
                    connectionName, sqlEx.Number);

                // Log security event for SQL failure
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{databaseName ?? "default"}",
                    Success = false,
                    IpAddress = clientIp,
                    ErrorMessage = $"SQL Error {sqlEx.Number}: Connection failed",
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["Database"] = databaseName ?? "default",
                        ["ErrorNumber"] = sqlEx.Number,
                        ["ErrorSeverity"] = sqlEx.Class,
                        ["IsTransient"] = IsTransientError(sqlEx)
                    }
                });

                activity?.SetStatus(ActivityStatusCode.Error, $"SQL Error {sqlEx.Number}");
                
                // Clean up connection if it was created
                connection?.Dispose();

                // Throw a sanitized exception
                throw new InvalidOperationException(
                    $"Failed to establish database connection to '{connectionName}'. " +
                    $"SQL Error {sqlEx.Number} occurred. Please check configuration and try again.",
                    sqlEx);
            }
            catch (OperationCanceledException)
            {
                // Log cancellation
                _logger.LogWarning(
                    "Database connection creation was cancelled for {ConnectionName}",
                    connectionName);

                // Security log for cancellation
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{databaseName ?? "default"}",
                    Success = false,
                    IpAddress = clientIp,
                    ErrorMessage = "Operation cancelled",
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["Database"] = databaseName ?? "default",
                        ["Reason"] = "UserCancellation"
                    }
                });

                activity?.SetStatus(ActivityStatusCode.Error, "Operation cancelled");
                
                // Clean up connection if it was created
                connection?.Dispose();

                // Re-throw cancellation
                throw;
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                _logger.LogError(ex, 
                    "Unexpected error occurred while creating connection to {ConnectionName}",
                    connectionName);

                // Log security event for general failure
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{databaseName ?? "default"}",
                    Success = false,
                    IpAddress = clientIp,
                    ErrorMessage = "Connection failed due to unexpected error",
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["Database"] = databaseName ?? "default",
                        ["ErrorType"] = ex.GetType().Name
                    }
                });

                activity?.SetStatus(ActivityStatusCode.Error, "Unexpected error");
                
                // Clean up connection if it was created
                connection?.Dispose();

                // Throw a sanitized exception
                throw new InvalidOperationException(
                    $"Failed to establish database connection to '{connectionName}'. " +
                    "An unexpected error occurred. Please contact support if the problem persists.",
                    ex);
            }
        }

        /// <summary>
        /// Masks server name for security logging
        /// </summary>
        /// <param name="dataSource">The data source/server name to mask</param>
        /// <returns>Masked server name</returns>
        private static string MaskServerName(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
                return "***";

            // Split by dots to handle FQDN
            var parts = dataSource.Split('.');
            
            if (parts.Length == 1)
            {
                // Simple server name - mask middle portion
                if (dataSource.Length <= 6)
                    return "***";
                
                return dataSource.Substring(0, 3) + "***";
            }
            
            // FQDN - keep first 3 chars of first part and last part
            var maskedParts = new List<string>();
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0)
                {
                    // First part - keep first 3 characters
                    maskedParts.Add(parts[i].Length > 3 
                        ? parts[i].Substring(0, 3) + "***" 
                        : "***");
                }
                else if (i == parts.Length - 1 || i == parts.Length - 2)
                {
                    // Keep last two parts (e.g., "windows.net")
                    maskedParts.Add(parts[i]);
                }
                else
                {
                    // Mask middle parts
                    maskedParts.Add("***");
                }
            }
            
            return string.Join(".", maskedParts);
        }

        /// <summary>
        /// Gets the client IP address from available sources
        /// </summary>
        /// <returns>Client IP address or fallback value</returns>
        private string GetClientIpAddress()
        {
            try
            {
                // Try to get from HTTP context first (if in web context)
                if (_httpContextAccessor?.HttpContext != null)
                {
                    var context = _httpContextAccessor.HttpContext;
                    
                    // Check X-Forwarded-For header (for proxies/load balancers)
                    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(forwardedFor))
                    {
                        // Take the first IP in the chain
                        var firstIp = forwardedFor.Split(',')[0].Trim();
                        if (IPAddress.TryParse(firstIp, out _))
                            return firstIp;
                    }
                    
                    // Check X-Real-IP header
                    var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
                        return realIp;
                    
                    // Get from connection
                    var connectionIp = context.Connection.RemoteIpAddress?.ToString();
                    if (!string.IsNullOrEmpty(connectionIp))
                        return connectionIp;
                }
                
                // Try to get local machine IP
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var localIp = host.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    ?.ToString();
                
                if (!string.IsNullOrEmpty(localIp))
                    return $"{localIp} (local)";
                
                // Fallback to machine name
                return $"{Environment.MachineName} (machine)";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to determine client IP address");
                return "Unknown";
            }
        }
    }
}