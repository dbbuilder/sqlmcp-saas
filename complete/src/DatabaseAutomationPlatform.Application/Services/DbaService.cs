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
/// Service implementation for DBA operations
/// </summary>
public class DbaService : IDbaService
{
    private readonly IStoredProcedureExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DbaService> _logger;

    public DbaService(
        IStoredProcedureExecutor executor,
        IUnitOfWork unitOfWork,
        ILogger<DbaService> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatabaseHealthResult> GetDatabaseHealthAsync(string database, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));

        _logger.LogInformation("Getting health metrics for database {Database}", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database)
        };

        var result = await _executor.ExecuteAsync("sp_GetDatabaseHealth", parameters, cancellationToken);
        
        var metrics = new Dictionary<string, HealthMetric>();
        var warnings = new List<string>();
        var criticalIssues = new List<string>();
        var overallStatus = "Healthy";

        foreach (DataRow row in result.Rows)
        {
            var metricName = row.Field<string>("MetricName") ?? string.Empty;
            var status = row.Field<string>("Status") ?? "Unknown";
            var value = row.Field<string>("MetricValue") ?? "0";
            
            var metric = new HealthMetric
            {
                Name = metricName,
                Value = value,
                Unit = row.Field<string>("Unit") ?? string.Empty,
                Status = status,
                Threshold = row.Field<decimal?>("Threshold")
            };

            metrics[metricName] = metric;

            switch (status.ToLower())
            {
                case "warning":
                    warnings.Add($"{metricName} is at warning level: {value}{metric.Unit}");
                    if (overallStatus == "Healthy") overallStatus = "Warning";
                    break;
                case "critical":
                    criticalIssues.Add($"{metricName} is critical: {value}{metric.Unit}");
                    overallStatus = "Critical";
                    break;
            }
        }

        return new DatabaseHealthResult
        {
            Status = overallStatus,
            Metrics = metrics,
            Warnings = warnings.ToArray(),
            CriticalIssues = criticalIssues.ToArray(),
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(string database, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));

        _logger.LogInformation("Analyzing performance for query on database {Database}", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Query", query)
        };

        var result = await _executor.ExecuteAsync("sp_AnalyzePerformance", parameters, cancellationToken);
        
        if (result.Rows.Count == 0)
        {
            throw new InvalidOperationException("No performance data returned");
        }

        var firstRow = result.Rows[0];
        var bottlenecks = new List<string>();
        var recommendations = new List<string>();

        foreach (DataRow row in result.Rows)
        {
            var bottleneck = row.Field<string>("Bottleneck");
            if (!string.IsNullOrEmpty(bottleneck))
            {
                bottlenecks.Add(bottleneck);
            }

            var recommendation = row.Field<string>("Recommendation");
            if (!string.IsNullOrEmpty(recommendation))
            {
                recommendations.Add(recommendation);
            }
        }

        return new PerformanceAnalysisResult
        {
            QueryHash = firstRow.Field<string>("QueryHash") ?? string.Empty,
            ExecutionTimeMs = firstRow.Field<long>("ExecutionTimeMs"),
            ExecutionPlan = firstRow.Field<string>("ExecutionPlan") ?? string.Empty,
            CpuTime = Convert.ToDouble(firstRow.Field<decimal>("CpuTime")),
            LogicalReads = Convert.ToDouble(firstRow.Field<decimal>("LogicalReads")),
            PhysicalReads = Convert.ToDouble(firstRow.Field<decimal>("PhysicalReads")),
            Bottlenecks = bottlenecks.ToArray(),
            Recommendations = recommendations.ToArray()
        };
    }

    public async Task<IndexRecommendationResult> GetIndexRecommendationsAsync(string database, string tableName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));

        _logger.LogInformation("Getting index recommendations for {Database}.{TableName}", database, tableName);

        // Get missing index recommendations
        var recommendationParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName)
        };

        var recommendationsResult = await _executor.ExecuteAsync("sp_GetIndexRecommendations", recommendationParams, cancellationToken);
        
        var recommendations = new List<IndexRecommendation>();
        foreach (DataRow row in recommendationsResult.Rows)
        {
            var columns = row.Field<string>("Columns")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var includedColumns = row.Field<string>("IncludedColumns")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            recommendations.Add(new IndexRecommendation
            {
                IndexName = row.Field<string>("IndexName") ?? string.Empty,
                Columns = columns,
                IncludedColumns = includedColumns,
                Reason = row.Field<string>("Reason") ?? string.Empty,
                EstimatedImprovement = Convert.ToDouble(row.Field<decimal>("EstimatedImprovement")),
                CreateStatement = row.Field<string>("CreateStatement") ?? string.Empty
            });
        }

        // Get current index usage stats
        var currentIndexParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName)
        };

        var currentIndexResult = await _executor.ExecuteAsync("sp_GetIndexUsageStats", currentIndexParams, cancellationToken);
        
        var currentIndexes = new List<IndexUsageStats>();
        foreach (DataRow row in currentIndexResult.Rows)
        {
            currentIndexes.Add(new IndexUsageStats
            {
                IndexName = row.Field<string>("IndexName") ?? string.Empty,
                UserSeeks = row.Field<long>("UserSeeks"),
                UserScans = row.Field<long>("UserScans"),
                UserLookups = row.Field<long>("UserLookups"),
                UserUpdates = row.Field<long>("UserUpdates"),
                LastUserSeek = row.Field<DateTime?>("LastUserSeek") != null 
                    ? new DateTimeOffset(row.Field<DateTime>("LastUserSeek"), TimeSpan.Zero) 
                    : null,
                LastUserScan = row.Field<DateTime?>("LastUserScan") != null 
                    ? new DateTimeOffset(row.Field<DateTime>("LastUserScan"), TimeSpan.Zero) 
                    : null
            });
        }

        return new IndexRecommendationResult
        {
            Recommendations = recommendations.ToArray(),
            CurrentIndexes = currentIndexes.ToArray()
        };
    }

    public async Task<BackupResult> BackupDatabaseAsync(string database, string backupPath, BackupType backupType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("Backup path is required", nameof(backupPath));

        _logger.LogInformation("Starting {BackupType} backup of database {Database} to {BackupPath}", 
            backupType, database, backupPath);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "BackupDatabase",
            EntityType = "Database",
            EntityId = database,
            AdditionalData = $"Type: {backupType}, Path: {backupPath}"
        };

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@BackupPath", backupPath),
                new SqlParameter("@BackupType", backupType.ToString()),
                new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@BackupSizeBytes", SqlDbType.BigInt) { Direction = ParameterDirection.Output },
                new SqlParameter("@DurationMs", SqlDbType.BigInt) { Direction = ParameterDirection.Output },
                new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, 4000) { Direction = ParameterDirection.Output }
            };

            await _executor.ExecuteNonQueryAsync("sp_BackupDatabase", parameters, cancellationToken);
            
            var success = (bool)parameters[3].Value;
            var sizeBytes = Convert.ToInt64(parameters[4].Value);
            var durationMs = Convert.ToInt64(parameters[5].Value);
            var errorMessage = parameters[6].Value as string;

            auditEvent.Success = success;
            auditEvent.Duration = (int)durationMs;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Backup failed: {errorMessage}");
            }

            _logger.LogInformation("Backup completed successfully. Size: {SizeMB}MB, Duration: {Duration}s", 
                sizeBytes / (1024 * 1024), durationMs / 1000);

            return new BackupResult
            {
                Success = true,
                BackupPath = backupPath,
                BackupSizeBytes = sizeBytes,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error backing up database {Database}", database);
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            return new BackupResult
            {
                Success = false,
                BackupPath = backupPath,
                Duration = DateTimeOffset.UtcNow - startTime,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RestoreResult> RestoreDatabaseAsync(string database, string backupPath, RestoreOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("Backup path is required", nameof(backupPath));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Starting restore of database {Database} from {BackupPath}", database, backupPath);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "RestoreDatabase",
            EntityType = "Database",
            EntityId = database,
            AdditionalData = $"Path: {backupPath}, WithReplace: {options.WithReplace}"
        };

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Database", options.NewDatabaseName ?? database),
                new SqlParameter("@BackupPath", backupPath),
                new SqlParameter("@WithReplace", options.WithReplace),
                new SqlParameter("@WithNoRecovery", options.WithNoRecovery),
                new SqlParameter("@DataFilePath", (object?)options.DataFilePath ?? DBNull.Value),
                new SqlParameter("@LogFilePath", (object?)options.LogFilePath ?? DBNull.Value),
                new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@DurationMs", SqlDbType.BigInt) { Direction = ParameterDirection.Output },
                new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, 4000) { Direction = ParameterDirection.Output }
            };

            await _executor.ExecuteNonQueryAsync("sp_RestoreDatabase", parameters, cancellationToken);
            
            var success = (bool)parameters[6].Value;
            var durationMs = Convert.ToInt64(parameters[7].Value);
            var errorMessage = parameters[8].Value as string;

            auditEvent.Success = success;
            auditEvent.Duration = (int)durationMs;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Restore failed: {errorMessage}");
            }

            _logger.LogInformation("Restore completed successfully. Duration: {Duration}s", durationMs / 1000);

            return new RestoreResult
            {
                Success = true,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database {Database}", database);
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            return new RestoreResult
            {
                Success = false,
                Duration = DateTimeOffset.UtcNow - startTime,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<DatabaseStatistics> GetStatisticsAsync(string database, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));

        _logger.LogInformation("Getting statistics for database {Database}", database);

        // Get overall database statistics
        var dbStatsParams = new[]
        {
            new SqlParameter("@Database", database)
        };

        var dbStatsResult = await _executor.ExecuteAsync("sp_GetDatabaseStatistics", dbStatsParams, cancellationToken);
        
        if (dbStatsResult.Rows.Count == 0)
        {
            throw new InvalidOperationException("No statistics returned for database");
        }

        var statsRow = dbStatsResult.Rows[0];
        var statistics = new DatabaseStatistics
        {
            SizeMB = statsRow.Field<long>("SizeMB"),
            DataSizeMB = statsRow.Field<long>("DataSizeMB"),
            IndexSizeMB = statsRow.Field<long>("IndexSizeMB"),
            TableCount = statsRow.Field<int>("TableCount"),
            IndexCount = statsRow.Field<int>("IndexCount"),
            ProcedureCount = statsRow.Field<int>("ProcedureCount"),
            ViewCount = statsRow.Field<int>("ViewCount"),
            UserCount = statsRow.Field<int>("UserCount")
        };

        // Get table-level statistics
        var tableStatsParams = new[]
        {
            new SqlParameter("@Database", database)
        };

        var tableStatsResult = await _executor.ExecuteAsync("sp_GetTableStatistics", tableStatsParams, cancellationToken);
        
        foreach (DataRow row in tableStatsResult.Rows)
        {
            var tableName = row.Field<string>("TableName") ?? string.Empty;
            statistics.Tables[tableName] = new TableStatistics
            {
                TableName = tableName,
                RowCount = row.Field<long>("RowCount"),
                DataSizeKB = row.Field<long>("DataSizeKB"),
                IndexSizeKB = row.Field<long>("IndexSizeKB"),
                LastUpdated = row.Field<DateTime?>("LastUpdated") != null 
                    ? new DateTimeOffset(row.Field<DateTime>("LastUpdated"), TimeSpan.Zero) 
                    : null
            };
        }

        return statistics;
    }

    public async Task<MaintenanceResult> RunMaintenanceAsync(string database, MaintenanceType type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));

        _logger.LogInformation("Running {MaintenanceType} maintenance on database {Database}", type, database);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "RunMaintenance",
            EntityType = "Database",
            EntityId = database,
            AdditionalData = $"Type: {type}"
        };

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@MaintenanceType", type.ToString())
            };

            var result = await _executor.ExecuteAsync("sp_RunMaintenance", parameters, cancellationToken);
            
            var tasksCompleted = new List<string>();
            var tasksFailed = new List<string>();

            foreach (DataRow row in result.Rows)
            {
                var taskName = row.Field<string>("TaskName") ?? string.Empty;
                var success = row.Field<bool>("Success");
                var message = row.Field<string>("Message") ?? string.Empty;

                if (success)
                {
                    tasksCompleted.Add(taskName);
                }
                else
                {
                    tasksFailed.Add($"{taskName}: {message}");
                }
            }

            var duration = DateTimeOffset.UtcNow - startTime;
            var overallSuccess = tasksFailed.Count == 0 || tasksCompleted.Count > tasksFailed.Count;

            auditEvent.Success = overallSuccess;
            auditEvent.Duration = (int)duration.TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            _logger.LogInformation("Maintenance completed. Tasks completed: {CompletedCount}, Failed: {FailedCount}", 
                tasksCompleted.Count, tasksFailed.Count);

            return new MaintenanceResult
            {
                Success = overallSuccess,
                Type = type,
                Duration = duration,
                TasksCompleted = tasksCompleted.ToArray(),
                TasksFailed = tasksFailed.ToArray(),
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running maintenance on database {Database}", database);
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            throw;
        }
    }
}