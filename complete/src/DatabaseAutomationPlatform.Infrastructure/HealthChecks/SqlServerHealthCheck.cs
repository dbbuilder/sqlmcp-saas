using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAutomationPlatform.Infrastructure.Data;

namespace DatabaseAutomationPlatform.Infrastructure.HealthChecks
{
    /// <summary>
    /// Health check for SQL Server connectivity and basic functionality
    /// </summary>
    public class SqlServerHealthCheck : IHealthCheck
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<SqlServerHealthCheck> _logger;
        private readonly string _connectionStringName;
        private const int TimeoutSeconds = 10;
        private const string HealthCheckQuery = @"
            SELECT 
                @@VERSION AS Version,
                @@SERVERNAME AS ServerName,
                DB_NAME() AS DatabaseName,
                SUSER_SNAME() AS LoginName,
                GETUTCDATE() AS ServerTimeUtc";

        public SqlServerHealthCheck(
            IDbConnectionFactory connectionFactory,
            ILogger<SqlServerHealthCheck> logger,
            string connectionStringName)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionStringName = connectionStringName ?? throw new ArgumentNullException(nameof(connectionStringName));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["ConnectionStringName"] = _connectionStringName
            };

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

                await using var connection = await _connectionFactory.CreateConnectionAsync(
                    _connectionStringName,
                    cts.Token);

                await using var command = connection.CreateCommand();
                command.CommandText = HealthCheckQuery;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = TimeoutSeconds;

                await using var reader = await command.ExecuteReaderAsync(cts.Token);
                
                if (await reader.ReadAsync(cts.Token))
                {
                    data["ServerVersion"] = reader["Version"]?.ToString() ?? "Unknown";
                    data["ServerName"] = reader["ServerName"]?.ToString() ?? "Unknown";
                    data["DatabaseName"] = reader["DatabaseName"]?.ToString() ?? "Unknown";
                    data["LoginName"] = reader["LoginName"]?.ToString() ?? "Unknown";
                    data["ServerTimeUtc"] = reader["ServerTimeUtc"]?.ToString() ?? "Unknown";
                }

                stopwatch.Stop();
                data["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds;
                data["ConnectionState"] = connection.State.ToString();

                // Check if connection pool is healthy
                var poolStats = await GetConnectionPoolStatistics(connection);
                if (poolStats != null)
                {
                    data["ConnectionPool"] = poolStats;
                }

                return HealthCheckResult.Healthy(
                    "SQL Server is accessible and functioning properly",
                    data);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                data["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds;
                data["TimeoutSeconds"] = TimeoutSeconds;

                _logger.LogWarning(
                    "SQL Server health check timed out after {TimeoutSeconds}s for connection {ConnectionStringName}",
                    TimeoutSeconds,
                    _connectionStringName);

                return HealthCheckResult.Unhealthy(
                    $"SQL Server health check timed out after {TimeoutSeconds}s",
                    data: data);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                data["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds;
                data["ErrorType"] = ex.GetType().Name;
                data["ErrorMessage"] = ex.Message;

                _logger.LogError(
                    ex,
                    "SQL Server health check failed for connection {ConnectionStringName}",
                    _connectionStringName);

                // Determine if this is a degraded or unhealthy state
                if (IsTransientError(ex))
                {
                    return HealthCheckResult.Degraded(
                        "SQL Server is experiencing transient issues",
                        exception: ex,
                        data: data);
                }

                return HealthCheckResult.Unhealthy(
                    "SQL Server is not accessible",
                    exception: ex,
                    data: data);
            }
        }

        private async Task<object> GetConnectionPoolStatistics(IDbConnection connection)
        {
            try
            {
                // Try to get SQL Server specific connection pool statistics
                if (connection is Microsoft.Data.SqlClient.SqlConnection sqlConnection)
                {
                    // Execute a query to get connection pool info from DMVs
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT 
                            COUNT(*) as ActiveConnections,
                            SUM(CASE WHEN status = 'sleeping' THEN 1 ELSE 0 END) as SleepingConnections,
                            MAX(connect_time) as OldestConnection,
                            AVG(DATEDIFF(SECOND, connect_time, GETDATE())) as AvgConnectionAgeSeconds
                        FROM sys.dm_exec_connections
                        WHERE client_net_address IS NOT NULL";
                    
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 5;

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        return new
                        {
                            ActiveConnections = reader["ActiveConnections"],
                            SleepingConnections = reader["SleepingConnections"],
                            OldestConnection = reader["OldestConnection"],
                            AvgConnectionAgeSeconds = reader["AvgConnectionAgeSeconds"]
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to retrieve connection pool statistics");
            }

            return null;
        }

        private bool IsTransientError(Exception exception)
        {
            // Check if this is a transient SQL error that might recover
            if (exception is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                // List of SQL error numbers that are considered transient
                var transientErrors = new[] 
                { 
                    -2,    // Timeout
                    20,    // Encryption not supported
                    64,    // Connection was successfully established but then server errors
                    233,   // Connection initialization error
                    10053, // Transport-level error
                    10054, // Transport-level error
                    10060, // Network timeout
                    40143, // Connection terminated
                    40197, // Service error
                    40501, // Service busy
                    40613, // Database unavailable
                    49918, // Cannot process request
                    49919, // Cannot process request
                    49920  // Cannot process request
                };

                return transientErrors.Contains(sqlEx.Number);
            }

            return false;
        }
    }
}