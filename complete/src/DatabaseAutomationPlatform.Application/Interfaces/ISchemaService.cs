namespace DatabaseAutomationPlatform.Application.Interfaces;

/// <summary>
/// Service interface for schema management operations
/// </summary>
public interface ISchemaService
{
    /// <summary>
    /// Gets schema information for a database object
    /// </summary>
    Task<SchemaInfo> GetSchemaInfoAsync(string database, ObjectType objectType, string? objectName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two schemas
    /// </summary>
    Task<SchemaComparisonResult> CompareSchemaAsync(string sourceDatabase, string targetDatabase, ComparisonOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates migration script between schemas
    /// </summary>
    Task<MigrationScript> GenerateMigrationAsync(string sourceDatabase, string targetDatabase, MigrationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documents database schema
    /// </summary>
    Task<SchemaDocumentation> GenerateDocumentationAsync(string database, DocumentationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes schema dependencies
    /// </summary>
    Task<DependencyAnalysisResult> AnalyzeDependenciesAsync(string database, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates schema against best practices
    /// </summary>
    Task<SchemaValidationResult> ValidateSchemaAsync(string database, ValidationRules rules, CancellationToken cancellationToken = default);
}

/// <summary>
/// Schema information
/// </summary>
public class SchemaInfo
{
    public Dictionary<string, TableSchema> Tables { get; set; } = new();
    public Dictionary<string, ViewSchema> Views { get; set; } = new();
    public Dictionary<string, ProcedureSchema> Procedures { get; set; } = new();
    public Dictionary<string, FunctionSchema> Functions { get; set; } = new();
    public Dictionary<string, IndexSchema> Indexes { get; set; } = new();
    public Dictionary<string, TriggerSchema> Triggers { get; set; } = new();
}

/// <summary>
/// Table schema
/// </summary>
public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public ColumnSchema[] Columns { get; set; } = Array.Empty<ColumnSchema>();
    public ConstraintSchema[] Constraints { get; set; } = Array.Empty<ConstraintSchema>();
    public IndexSchema[] Indexes { get; set; } = Array.Empty<IndexSchema>();
    public TriggerSchema[] Triggers { get; set; } = Array.Empty<TriggerSchema>();
    public string? Description { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ModifiedDate { get; set; }
}

/// <summary>
/// Column schema
/// </summary>
public class ColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public int OrdinalPosition { get; set; }
}

/// <summary>
/// Constraint schema
/// </summary>
public class ConstraintSchema
{
    public string Name { get; set; } = string.Empty;
    public ConstraintType Type { get; set; }
    public string[] Columns { get; set; } = Array.Empty<string>();
    public string? ReferencedTable { get; set; }
    public string[]? ReferencedColumns { get; set; }
    public string? CheckClause { get; set; }
}

/// <summary>
/// View schema
/// </summary>
public class ViewSchema
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string Definition { get; set; } = string.Empty;
    public ColumnSchema[] Columns { get; set; } = Array.Empty<ColumnSchema>();
    public string? Description { get; set; }
    public bool IsIndexed { get; set; }
}

/// <summary>
/// Stored procedure schema
/// </summary>
public class ProcedureSchema
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string Definition { get; set; } = string.Empty;
    public ParameterSchema[] Parameters { get; set; } = Array.Empty<ParameterSchema>();
    public string? Description { get; set; }
}

/// <summary>
/// Function schema
/// </summary>
public class FunctionSchema
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string Definition { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public ParameterSchema[] Parameters { get; set; } = Array.Empty<ParameterSchema>();
    public FunctionType Type { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Parameter schema
/// </summary>
public class ParameterSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Index schema
/// </summary>
public class IndexSchema
{
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string[] Columns { get; set; } = Array.Empty<string>();
    public string[] IncludedColumns { get; set; } = Array.Empty<string>();
    public IndexType Type { get; set; }
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsDisabled { get; set; }
    public string? FilterDefinition { get; set; }
}

/// <summary>
/// Trigger schema
/// </summary>
public class TriggerSchema
{
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public TriggerType Type { get; set; }
    public TriggerEvent[] Events { get; set; } = Array.Empty<TriggerEvent>();
    public bool IsDisabled { get; set; }
}

/// <summary>
/// Schema comparison result
/// </summary>
public class SchemaComparisonResult
{
    public SchemaDifference[] Differences { get; set; } = Array.Empty<SchemaDifference>();
    public int TotalDifferences => Differences.Length;
    public Dictionary<DifferenceType, int> DifferenceCounts { get; set; } = new();
}

/// <summary>
/// Schema difference
/// </summary>
public class SchemaDifference
{
    public DifferenceType Type { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string? SourceDefinition { get; set; }
    public string? TargetDefinition { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Migration script
/// </summary>
public class MigrationScript
{
    public string Script { get; set; } = string.Empty;
    public MigrationStep[] Steps { get; set; } = Array.Empty<MigrationStep>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public bool RequiresDataMigration { get; set; }
    public long EstimatedDurationSeconds { get; set; }
}

/// <summary>
/// Migration step
/// </summary>
public class MigrationStep
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public bool IsReversible { get; set; }
    public string? RollbackScript { get; set; }
}

/// <summary>
/// Schema documentation
/// </summary>
public class SchemaDocumentation
{
    public string Format { get; set; } = "Markdown";
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Sections { get; set; } = new();
    public DateTimeOffset GeneratedAt { get; set; }
}

/// <summary>
/// Dependency analysis result
/// </summary>
public class DependencyAnalysisResult
{
    public string ObjectName { get; set; } = string.Empty;
    public ObjectType ObjectType { get; set; }
    public Dependency[] Dependencies { get; set; } = Array.Empty<Dependency>();
    public Dependency[] Dependents { get; set; } = Array.Empty<Dependency>();
    public string[] ImpactAnalysis { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Dependency information
/// </summary>
public class Dependency
{
    public string Name { get; set; } = string.Empty;
    public ObjectType Type { get; set; }
    public DependencyLevel Level { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Schema validation result
/// </summary>
public class SchemaValidationResult
{
    public bool IsValid { get; set; }
    public ValidationIssue[] Issues { get; set; } = Array.Empty<ValidationIssue>();
    public Dictionary<string, int> IssueCounts { get; set; } = new();
}

/// <summary>
/// Validation issue
/// </summary>
public class ValidationIssue
{
    public IssueSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

// Enums
public enum ObjectType
{
    Table,
    View,
    Procedure,
    Function,
    Index,
    Trigger,
    Constraint
}

public enum ConstraintType
{
    PrimaryKey,
    ForeignKey,
    Unique,
    Check,
    Default
}

public enum FunctionType
{
    Scalar,
    TableValued,
    InlineTableValued
}

public enum IndexType
{
    Clustered,
    NonClustered,
    ColumnStore,
    FullText,
    Spatial,
    XML
}

public enum TriggerType
{
    After,
    Instead
}

public enum TriggerEvent
{
    Insert,
    Update,
    Delete
}

public enum DifferenceType
{
    Added,
    Removed,
    Modified
}

public enum DependencyLevel
{
    Direct,
    Indirect
}

public enum IssueSeverity
{
    Error,
    Warning,
    Info
}

// Options classes
public class ComparisonOptions
{
    public bool IncludeTables { get; set; } = true;
    public bool IncludeViews { get; set; } = true;
    public bool IncludeProcedures { get; set; } = true;
    public bool IncludeFunctions { get; set; } = true;
    public bool IncludeIndexes { get; set; } = true;
    public bool IncludeTriggers { get; set; } = true;
    public bool IgnoreWhitespace { get; set; } = true;
    public bool IgnoreComments { get; set; } = true;
}

public class MigrationOptions
{
    public bool IncludeDropStatements { get; set; } = true;
    public bool PreserveData { get; set; } = true;
    public bool GenerateRollback { get; set; } = true;
    public bool UseTransactions { get; set; } = true;
    public bool CheckDependencies { get; set; } = true;
}

public class DocumentationOptions
{
    public string Format { get; set; } = "Markdown";
    public bool IncludeDescriptions { get; set; } = true;
    public bool IncludeExamples { get; set; } = true;
    public bool IncludeDependencies { get; set; } = true;
    public bool IncludePermissions { get; set; } = true;
}

public class ValidationRules
{
    public bool CheckNamingConventions { get; set; } = true;
    public bool CheckDataTypes { get; set; } = true;
    public bool CheckIndexes { get; set; } = true;
    public bool CheckConstraints { get; set; } = true;
    public bool CheckSecurity { get; set; } = true;
    public bool CheckPerformance { get; set; } = true;
}