namespace DatabaseAutomationPlatform.Application.Interfaces;

/// <summary>
/// Service interface for SQL Developer operations
/// </summary>
public interface IDeveloperService
{
    /// <summary>
    /// Executes a read-only SQL query
    /// </summary>
    Task<QueryResult> ExecuteQueryAsync(string database, string query, int timeoutSeconds = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL command (INSERT, UPDATE, DELETE)
    /// </summary>
    Task<CommandResult> ExecuteCommandAsync(string database, string command, bool useTransaction = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates SQL code based on natural language description
    /// </summary>
    Task<SqlGenerationResult> GenerateSqlAsync(string database, string description, string context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes a SQL query for better performance
    /// </summary>
    Task<QueryOptimizationResult> OptimizeQueryAsync(string database, string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates SQL syntax without executing
    /// </summary>
    Task<SqlValidationResult> ValidateSqlAsync(string database, string sql, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a SQL query execution
/// </summary>
public class QueryResult
{
    public string[] Columns { get; set; } = Array.Empty<string>();
    public object[][] Rows { get; set; } = Array.Empty<object[]>();
    public int RowCount { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ExecutionPlan { get; set; }
}

/// <summary>
/// Result of a SQL command execution
/// </summary>
public class CommandResult
{
    public int AffectedRows { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Dictionary<string, object>? OutputParameters { get; set; }
}

/// <summary>
/// Result of SQL generation
/// </summary>
public class SqlGenerationResult
{
    public string GeneratedSql { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Result of query optimization
/// </summary>
public class QueryOptimizationResult
{
    public string OptimizedQuery { get; set; } = string.Empty;
    public string[] Recommendations { get; set; } = Array.Empty<string>();
    public double EstimatedImprovement { get; set; }
    public string OriginalPlan { get; set; } = string.Empty;
    public string OptimizedPlan { get; set; } = string.Empty;
}

/// <summary>
/// Result of SQL validation
/// </summary>
public class SqlValidationResult
{
    public bool IsValid { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Suggestions { get; set; } = new();
}