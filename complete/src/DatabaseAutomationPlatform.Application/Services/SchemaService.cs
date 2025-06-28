using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Application.Services;

/// <summary>
/// Service implementation for database schema operations
/// </summary>
public class SchemaService : ISchemaService
{
    private readonly IStoredProcedureExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SchemaService> _logger;

    public SchemaService(
        IStoredProcedureExecutor executor,
        IUnitOfWork unitOfWork,
        ILogger<SchemaService> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SchemaInfo> GetSchemaInfoAsync(string database, ObjectType objectType, string? objectName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));

        _logger.LogInformation("Getting schema information for {ObjectType} {ObjectName} in database {Database}", 
            objectType, objectName ?? "all", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@ObjectType", objectType.ToString()),
            new SqlParameter("@ObjectName", (object?)objectName ?? DBNull.Value)
        };

        var result = await _executor.ExecuteAsync("sp_GetSchemaInfo", parameters, cancellationToken);
        
        var schemaInfo = new SchemaInfo();

        // Process table information
        if (objectType == ObjectType.Table || objectType == ObjectType.Constraint)
        {
            var tableGroups = result.AsEnumerable()
                .GroupBy(r => r.Field<string>("TableName") ?? string.Empty);

            foreach (var tableGroup in tableGroups)
            {
                var tableName = tableGroup.Key;
                var columns = new List<ColumnSchema>();
                
                foreach (var row in tableGroup)
                {
                    var columnName = row.Field<string>("ColumnName") ?? string.Empty;
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        columns.Add(new ColumnSchema
                        {
                            Name = columnName,
                            DataType = row.Field<string>("DataType") ?? string.Empty,
                            MaxLength = row.Field<int?>("MaxLength"),
                            IsNullable = row.Field<bool>("IsNullable"),
                            IsIdentity = row.Field<bool>("IsIdentity"),
                            DefaultValue = row.Field<string>("DefaultValue"),
                            OrdinalPosition = row.Field<int>("OrdinalPosition")
                        });
                    }
                }

                schemaInfo.Tables[tableName] = new TableSchema
                {
                    Name = tableName,
                    Schema = tableGroup.First().Field<string>("Schema") ?? "dbo",
                    Columns = columns.ToArray(),
                    CreatedDate = tableGroup.First().Field<DateTimeOffset>("CreatedDate"),
                    ModifiedDate = tableGroup.First().Field<DateTimeOffset>("ModifiedDate")
                };
            }
        }

        return schemaInfo;
    }

    public async Task<SchemaComparisonResult> CompareSchemaAsync(string sourceDatabase, string targetDatabase, ComparisonOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceDatabase)) throw new ArgumentException("Source database name is required", nameof(sourceDatabase));
        if (string.IsNullOrWhiteSpace(targetDatabase)) throw new ArgumentException("Target database name is required", nameof(targetDatabase));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Comparing schema between {SourceDatabase} and {TargetDatabase}", sourceDatabase, targetDatabase);

        var parameters = new[]
        {
            new SqlParameter("@SourceDatabase", sourceDatabase),
            new SqlParameter("@TargetDatabase", targetDatabase),
            new SqlParameter("@Options", JsonSerializer.Serialize(options))
        };

        var result = await _executor.ExecuteAsync("sp_CompareSchema", parameters, cancellationToken);
        
        var differences = new List<SchemaDifference>();
        
        foreach (DataRow row in result.Rows)
        {
            var diffType = row.Field<string>("DifferenceType") ?? string.Empty;
            var difference = new SchemaDifference
            {
                Type = Enum.Parse<DifferenceType>(diffType, true),
                ObjectType = row.Field<string>("ObjectType") ?? string.Empty,
                ObjectName = row.Field<string>("ObjectName") ?? string.Empty,
                SourceDefinition = row.Field<string>("SourceDefinition"),
                TargetDefinition = row.Field<string>("TargetDefinition"),
                Description = row.Field<string>("Details") ?? string.Empty
            };
            
            differences.Add(difference);
        }

        // Count differences by type
        var differenceCounts = differences
            .GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SchemaComparisonResult
        {
            Differences = differences.ToArray(),
            DifferenceCounts = differenceCounts
        };
    }

    public async Task<MigrationScript> GenerateMigrationAsync(string sourceDatabase, string targetDatabase, MigrationOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceDatabase)) throw new ArgumentException("Source database name is required", nameof(sourceDatabase));
        if (string.IsNullOrWhiteSpace(targetDatabase)) throw new ArgumentException("Target database name is required", nameof(targetDatabase));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Generating migration script from {SourceDatabase} to {TargetDatabase}", sourceDatabase, targetDatabase);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "GenerateMigration",
            EntityType = "Schema",
            EntityId = $"{sourceDatabase}→{targetDatabase}",
            AdditionalData = JsonSerializer.Serialize(options)
        };

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@SourceDatabase", sourceDatabase),
                new SqlParameter("@TargetDatabase", targetDatabase),
                new SqlParameter("@Options", JsonSerializer.Serialize(options))
            };

            var result = await _executor.ExecuteAsync("sp_GenerateMigration", parameters, cancellationToken);
            
            if (result.Rows.Count == 0)
            {
                throw new InvalidOperationException("No migration script generated");
            }

            var row = result.Rows[0];
            var warnings = row.Field<string>("Warnings")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            // Parse migration steps
            var steps = new List<MigrationStep>();
            if (result.Rows.Count > 1)
            {
                for (int i = 1; i < result.Rows.Count; i++)
                {
                    var stepRow = result.Rows[i];
                    steps.Add(new MigrationStep
                    {
                        Order = stepRow.Field<int>("Order"),
                        Description = stepRow.Field<string>("Description") ?? string.Empty,
                        Script = stepRow.Field<string>("Script") ?? string.Empty,
                        IsReversible = stepRow.Field<bool>("IsReversible"),
                        RollbackScript = stepRow.Field<string>("RollbackScript")
                    });
                }
            }

            auditEvent.Success = true;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            return new MigrationScript
            {
                Script = row.Field<string>("UpScript") ?? string.Empty,
                Steps = steps.ToArray(),
                Warnings = warnings,
                RequiresDataMigration = row.Field<bool>("RequiresDataMigration"),
                EstimatedDurationSeconds = row.Field<long>("EstimatedDurationSeconds")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating migration script");
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            throw;
        }
    }

    public async Task<SchemaDocumentation> GenerateDocumentationAsync(string database, DocumentationOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Generating documentation for database {Database} in {Format} format", database, options.Format);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Options", JsonSerializer.Serialize(options))
        };

        var result = await _executor.ExecuteAsync("sp_GenerateDocumentation", parameters, cancellationToken);
        
        var sections = new List<(int order, string content)>();
        
        foreach (DataRow row in result.Rows)
        {
            var order = row.Field<int>("Order");
            var content = row.Field<string>("Content") ?? string.Empty;
            sections.Add((order, content));
        }

        // Combine sections in order
        var fullContent = string.Join("\n\n", sections.OrderBy(s => s.order).Select(s => s.content));

        // Build sections dictionary
        var sectionsDict = new Dictionary<string, string>();
        foreach (DataRow row in result.Rows)
        {
            var section = row.Field<string>("Section") ?? string.Empty;
            var content = row.Field<string>("Content") ?? string.Empty;
            sectionsDict[section] = content;
        }

        return new SchemaDocumentation
        {
            Format = options.Format,
            Content = fullContent,
            Sections = sectionsDict,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<DependencyAnalysisResult> AnalyzeDependenciesAsync(string database, string objectName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name is required", nameof(objectName));

        _logger.LogInformation("Analyzing dependencies for {ObjectName} in database {Database}", objectName, database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@ObjectName", objectName)
        };

        var result = await _executor.ExecuteAsync("sp_AnalyzeDependencies", parameters, cancellationToken);
        
        var dependencies = new List<Dependency>();
        var dependents = new List<Dependency>();
        
        foreach (DataRow row in result.Rows)
        {
            var dependentObject = row.Field<string>("DependentObject") ?? string.Empty;
            var referencedObject = row.Field<string>("ReferencedObject") ?? string.Empty;
            
            if (dependentObject.Equals(objectName, StringComparison.OrdinalIgnoreCase))
            {
                // This is a dependency of our object
                dependencies.Add(new Dependency
                {
                    Name = referencedObject,
                    Type = Enum.Parse<ObjectType>(row.Field<string>("ReferencedType") ?? "Table", true),
                    Level = row.Field<int>("Level") == 1 ? DependencyLevel.Direct : DependencyLevel.Indirect,
                    Reason = row.Field<string>("DependencyType") ?? string.Empty
                });
            }
            else if (referencedObject.Equals(objectName, StringComparison.OrdinalIgnoreCase))
            {
                // This object depends on our object
                dependents.Add(new Dependency
                {
                    Name = dependentObject,
                    Type = Enum.Parse<ObjectType>(row.Field<string>("DependentType") ?? "Table", true),
                    Level = row.Field<int>("Level") == 1 ? DependencyLevel.Direct : DependencyLevel.Indirect,
                    Reason = row.Field<string>("DependencyType") ?? string.Empty
                });
            }
        }

        // Get object type
        var objectTypeParam = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@ObjectName", objectName)
        };
        var typeResult = await _executor.ExecuteAsync("sp_GetObjectType", objectTypeParam, cancellationToken);
        var objectType = ObjectType.Table;
        if (typeResult.Rows.Count > 0)
        {
            var typeStr = typeResult.Rows[0].Field<string>("ObjectType") ?? "Table";
            objectType = Enum.Parse<ObjectType>(typeStr, true);
        }

        return new DependencyAnalysisResult
        {
            ObjectName = objectName,
            ObjectType = objectType,
            Dependencies = dependencies.ToArray(),
            Dependents = dependents.ToArray(),
            ImpactAnalysis = new[] { $"Modifying {objectName} will affect {dependents.Count} dependent objects" }
        };
    }

    public async Task<SchemaValidationResult> ValidateSchemaAsync(string database, ValidationRules rules, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        _logger.LogInformation("Validating schema for database {Database}", database);

        var parameters = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Rules", JsonSerializer.Serialize(rules))
        };

        var result = await _executor.ExecuteAsync("sp_ValidateSchema", parameters, cancellationToken);
        
        var issues = new List<ValidationIssue>();
        
        foreach (DataRow row in result.Rows)
        {
            var severity = row.Field<string>("Severity") ?? "Info";
            var issue = new ValidationIssue
            {
                Severity = Enum.Parse<IssueSeverity>(severity, true),
                Category = row.Field<string>("RuleName") ?? string.Empty,
                ObjectName = row.Field<string>("ObjectName") ?? string.Empty,
                Issue = row.Field<string>("Message") ?? string.Empty,
                Recommendation = row.Field<string>("Recommendation") ?? string.Empty
            };
            
            issues.Add(issue);
        }

        var hasErrors = issues.Any(i => i.Severity == IssueSeverity.Error);

        // Count issues by category
        var issueCounts = issues
            .GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SchemaValidationResult
        {
            IsValid = !hasErrors,
            Issues = issues.ToArray(),
            IssueCounts = issueCounts
        };
    }
}