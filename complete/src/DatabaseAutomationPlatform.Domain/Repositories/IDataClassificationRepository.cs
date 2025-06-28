using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Repositories;

/// <summary>
/// Repository interface for data classification operations
/// </summary>
public interface IDataClassificationRepository : IRepository<DataClassification, int>
{
    /// <summary>
    /// Get classifications by database
    /// </summary>
    Task<IEnumerable<DataClassification>> GetByDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get classifications by table
    /// </summary>
    Task<IEnumerable<DataClassification>> GetByTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get classifications by column
    /// </summary>
    Task<DataClassification?> GetByColumnAsync(string databaseName, string tableName, string columnName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get classifications by sensitivity level
    /// </summary>
    Task<IEnumerable<DataClassification>> GetBySensitivityLevelAsync(DataSensitivityLevel level, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get classifications requiring encryption
    /// </summary>
    Task<IEnumerable<DataClassification>> GetRequiringEncryptionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update classification for a column
    /// </summary>
    Task<bool> UpdateClassificationAsync(
        string databaseName, 
        string tableName, 
        string columnName, 
        DataSensitivityLevel newLevel, 
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk update classifications
    /// </summary>
    Task<int> BulkUpdateAsync(IEnumerable<DataClassification> classifications, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unclassified columns
    /// </summary>
    Task<IEnumerable<UnclassifiedColumn>> GetUnclassifiedColumnsAsync(string databaseName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an unclassified column
/// </summary>
public class UnclassifiedColumn
{
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}