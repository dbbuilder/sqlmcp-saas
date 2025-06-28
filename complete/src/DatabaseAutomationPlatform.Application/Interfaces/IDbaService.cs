namespace DatabaseAutomationPlatform.Application.Interfaces;

/// <summary>
/// Service interface for DBA operations
/// </summary>
public interface IDbaService
{
    /// <summary>
    /// Gets database health metrics
    /// </summary>
    Task<DatabaseHealthResult> GetDatabaseHealthAsync(string database, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes query performance
    /// </summary>
    Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(string database, string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets index recommendations
    /// </summary>
    Task<IndexRecommendationResult> GetIndexRecommendationsAsync(string database, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs database backup
    /// </summary>
    Task<BackupResult> BackupDatabaseAsync(string database, string backupPath, BackupType backupType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores database from backup
    /// </summary>
    Task<RestoreResult> RestoreDatabaseAsync(string database, string backupPath, RestoreOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets database statistics
    /// </summary>
    Task<DatabaseStatistics> GetStatisticsAsync(string database, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manages database maintenance tasks
    /// </summary>
    Task<MaintenanceResult> RunMaintenanceAsync(string database, MaintenanceType type, CancellationToken cancellationToken = default);
}

/// <summary>
/// Database health check result
/// </summary>
public class DatabaseHealthResult
{
    public string Status { get; set; } = "Unknown";
    public Dictionary<string, HealthMetric> Metrics { get; set; } = new();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public string[] CriticalIssues { get; set; } = Array.Empty<string>();
    public DateTimeOffset CheckedAt { get; set; }
}

/// <summary>
/// Health metric detail
/// </summary>
public class HealthMetric
{
    public string Name { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
    public double? Threshold { get; set; }
}

/// <summary>
/// Performance analysis result
/// </summary>
public class PerformanceAnalysisResult
{
    public string QueryHash { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public string ExecutionPlan { get; set; } = string.Empty;
    public double CpuTime { get; set; }
    public double LogicalReads { get; set; }
    public double PhysicalReads { get; set; }
    public string[] Bottlenecks { get; set; } = Array.Empty<string>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Index recommendation result
/// </summary>
public class IndexRecommendationResult
{
    public IndexRecommendation[] Recommendations { get; set; } = Array.Empty<IndexRecommendation>();
    public IndexUsageStats[] CurrentIndexes { get; set; } = Array.Empty<IndexUsageStats>();
}

/// <summary>
/// Index recommendation
/// </summary>
public class IndexRecommendation
{
    public string IndexName { get; set; } = string.Empty;
    public string[] Columns { get; set; } = Array.Empty<string>();
    public string[] IncludedColumns { get; set; } = Array.Empty<string>();
    public string Reason { get; set; } = string.Empty;
    public double EstimatedImprovement { get; set; }
    public string CreateStatement { get; set; } = string.Empty;
}

/// <summary>
/// Index usage statistics
/// </summary>
public class IndexUsageStats
{
    public string IndexName { get; set; } = string.Empty;
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
    public DateTimeOffset? LastUserSeek { get; set; }
    public DateTimeOffset? LastUserScan { get; set; }
}

/// <summary>
/// Backup result
/// </summary>
public class BackupResult
{
    public bool Success { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Restore result
/// </summary>
public class RestoreResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Database statistics
/// </summary>
public class DatabaseStatistics
{
    public long SizeMB { get; set; }
    public long DataSizeMB { get; set; }
    public long IndexSizeMB { get; set; }
    public int TableCount { get; set; }
    public int IndexCount { get; set; }
    public int ProcedureCount { get; set; }
    public int ViewCount { get; set; }
    public int UserCount { get; set; }
    public Dictionary<string, TableStatistics> Tables { get; set; } = new();
}

/// <summary>
/// Table statistics
/// </summary>
public class TableStatistics
{
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public long DataSizeKB { get; set; }
    public long IndexSizeKB { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
}

/// <summary>
/// Maintenance result
/// </summary>
public class MaintenanceResult
{
    public bool Success { get; set; }
    public MaintenanceType Type { get; set; }
    public TimeSpan Duration { get; set; }
    public string[] TasksCompleted { get; set; } = Array.Empty<string>();
    public string[] TasksFailed { get; set; } = Array.Empty<string>();
    public DateTimeOffset CompletedAt { get; set; }
}

/// <summary>
/// Backup type
/// </summary>
public enum BackupType
{
    Full,
    Differential,
    TransactionLog
}

/// <summary>
/// Restore options
/// </summary>
public class RestoreOptions
{
    public bool WithReplace { get; set; }
    public bool WithNoRecovery { get; set; }
    public string? NewDatabaseName { get; set; }
    public string? DataFilePath { get; set; }
    public string? LogFilePath { get; set; }
}

/// <summary>
/// Maintenance type
/// </summary>
public enum MaintenanceType
{
    IndexRebuild,
    IndexReorganize,
    UpdateStatistics,
    CheckDB,
    CleanupHistory
}