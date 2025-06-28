using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for data classifications
/// </summary>
public class DataClassificationRepository : BaseRepository<DataClassification, int>, IDataClassificationRepository
{
    public DataClassificationRepository(IStoredProcedureExecutor executor, ILogger<DataClassificationRepository> logger)
        : base(executor, logger, "DataClassification")
    {
    }

    public async Task<IEnumerable<DataClassification>> GetByDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data classifications for database: {DatabaseName}", databaseName);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName)
        };

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetByDatabase", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DataClassification>> GetByTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data classifications for table: {DatabaseName}.{TableName}", databaseName, tableName);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName),
            new SqlParameter("@TableName", tableName)
        };

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetByTable", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<DataClassification?> GetByColumnAsync(string databaseName, string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data classification for column: {DatabaseName}.{TableName}.{ColumnName}", 
            databaseName, tableName, columnName);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@ColumnName", columnName)
        };

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetByColumn", parameters, cancellationToken);
        
        if (result.Rows.Count == 0)
        {
            return null;
        }

        return MapFromDataRow(result.Rows[0]);
    }

    public async Task<IEnumerable<DataClassification>> GetBySensitivityLevelAsync(DataSensitivityLevel level, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data classifications by sensitivity level: {Level}", level);

        var parameters = new[]
        {
            new SqlParameter("@SensitivityLevel", level.ToString())
        };

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetBySensitivityLevel", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DataClassification>> GetRequiringEncryptionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data classifications requiring encryption");

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetRequiringEncryption", Array.Empty<SqlParameter>(), cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<bool> UpdateClassificationAsync(
        string databaseName, 
        string tableName, 
        string columnName, 
        DataSensitivityLevel newLevel, 
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating classification for {DatabaseName}.{TableName}.{ColumnName} to {NewLevel}", 
            databaseName, tableName, columnName, newLevel);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@ColumnName", columnName),
            new SqlParameter("@NewSensitivityLevel", newLevel.ToString()),
            new SqlParameter("@Notes", (object?)notes ?? DBNull.Value),
            new SqlParameter("@UpdatedAt", DateTimeOffset.UtcNow),
            new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output }
        };

        await _executor.ExecuteNonQueryAsync("sp_DataClassification_UpdateLevel", parameters, cancellationToken);
        
        var success = (bool)parameters[6].Value;
        
        if (success)
        {
            _logger.LogInformation("Classification updated successfully");
        }
        else
        {
            _logger.LogWarning("Failed to update classification");
        }

        return success;
    }

    public async Task<int> BulkUpdateAsync(IEnumerable<DataClassification> classifications, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bulk updating {Count} data classifications", classifications.Count());

        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("DatabaseName", typeof(string));
        dataTable.Columns.Add("SchemaName", typeof(string));
        dataTable.Columns.Add("TableName", typeof(string));
        dataTable.Columns.Add("ColumnName", typeof(string));
        dataTable.Columns.Add("DataType", typeof(string));
        dataTable.Columns.Add("SensitivityLevel", typeof(string));
        dataTable.Columns.Add("InformationType", typeof(string));
        dataTable.Columns.Add("EncryptionRequired", typeof(bool));
        dataTable.Columns.Add("MaskingRequired", typeof(bool));
        dataTable.Columns.Add("RetentionDays", typeof(int));
        dataTable.Columns.Add("Notes", typeof(string));

        foreach (var classification in classifications)
        {
            dataTable.Rows.Add(
                classification.Id,
                classification.DatabaseName,
                classification.SchemaName,
                classification.TableName,
                classification.ColumnName,
                classification.DataType,
                classification.SensitivityLevel.ToString(),
                classification.InformationType,
                classification.PrivacyRequirements.EncryptionRequired,
                classification.PrivacyRequirements.MaskingRequired,
                classification.PrivacyRequirements.RetentionDays ?? DBNull.Value,
                classification.Notes ?? DBNull.Value
            );
        }

        var parameters = new[]
        {
            new SqlParameter("@Classifications", SqlDbType.Structured)
            {
                TypeName = "dbo.DataClassificationType",
                Value = dataTable
            },
            new SqlParameter("@UpdatedCount", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        await _executor.ExecuteNonQueryAsync("sp_DataClassification_BulkUpdate", parameters, cancellationToken);
        
        var updatedCount = (int)parameters[1].Value;
        _logger.LogInformation("Bulk updated {UpdatedCount} classifications", updatedCount);

        return updatedCount;
    }

    public async Task<IEnumerable<UnclassifiedColumn>> GetUnclassifiedColumnsAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting unclassified columns for database: {DatabaseName}", databaseName);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName)
        };

        var result = await _executor.ExecuteAsync("sp_DataClassification_GetUnclassified", parameters, cancellationToken);
        
        return result.Rows.Cast<DataRow>().Select(row => new UnclassifiedColumn
        {
            DatabaseName = row.Field<string>("DatabaseName") ?? string.Empty,
            SchemaName = row.Field<string>("SchemaName") ?? string.Empty,
            TableName = row.Field<string>("TableName") ?? string.Empty,
            ColumnName = row.Field<string>("ColumnName") ?? string.Empty,
            DataType = row.Field<string>("DataType") ?? string.Empty,
            IsNullable = row.Field<bool>("IsNullable"),
            MaxLength = row.Field<int?>("MaxLength")
        });
    }

    protected override DataClassification MapFromDataRow(DataRow row)
    {
        return new DataClassification
        {
            Id = row.Field<int>("Id"),
            DatabaseName = row.Field<string>("DatabaseName") ?? string.Empty,
            SchemaName = row.Field<string>("SchemaName") ?? string.Empty,
            TableName = row.Field<string>("TableName") ?? string.Empty,
            ColumnName = row.Field<string>("ColumnName") ?? string.Empty,
            DataType = row.Field<string>("DataType") ?? string.Empty,
            SensitivityLevel = Enum.Parse<DataSensitivityLevel>(row.Field<string>("SensitivityLevel") ?? "Public"),
            InformationType = row.Field<string>("InformationType") ?? string.Empty,
            DiscoveredAt = row.Field<DateTimeOffset>("DiscoveredAt"),
            LastValidated = row.Field<DateTimeOffset>("LastValidated"),
            ValidationMethod = row.Field<string>("ValidationMethod") ?? string.Empty,
            ConfidenceScore = row.Field<decimal>("ConfidenceScore"),
            IsSystemClassified = row.Field<bool>("IsSystemClassified"),
            Notes = row.Field<string>("Notes"),
            PrivacyRequirements = new PrivacyRequirements
            {
                EncryptionRequired = row.Field<bool>("EncryptionRequired"),
                MaskingRequired = row.Field<bool>("MaskingRequired"),
                RetentionDays = row.Field<int?>("RetentionDays"),
                AllowedCountries = row.Field<string>("AllowedCountries")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                RestrictedCountries = row.Field<string>("RestrictedCountries")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                RequiresConsent = row.Field<bool>("RequiresConsent"),
                ConsentPurposes = row.Field<string>("ConsentPurposes")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>()
            }
        };
    }

    protected override SqlParameter[] GetInsertParameters(DataClassification entity)
    {
        var allowedCountries = entity.PrivacyRequirements.AllowedCountries.Any() 
            ? string.Join(",", entity.PrivacyRequirements.AllowedCountries) 
            : null;
        var restrictedCountries = entity.PrivacyRequirements.RestrictedCountries.Any() 
            ? string.Join(",", entity.PrivacyRequirements.RestrictedCountries) 
            : null;
        var consentPurposes = entity.PrivacyRequirements.ConsentPurposes.Any() 
            ? string.Join(",", entity.PrivacyRequirements.ConsentPurposes) 
            : null;

        return new[]
        {
            new SqlParameter("@DatabaseName", entity.DatabaseName),
            new SqlParameter("@SchemaName", entity.SchemaName),
            new SqlParameter("@TableName", entity.TableName),
            new SqlParameter("@ColumnName", entity.ColumnName),
            new SqlParameter("@DataType", entity.DataType),
            new SqlParameter("@SensitivityLevel", entity.SensitivityLevel.ToString()),
            new SqlParameter("@InformationType", entity.InformationType),
            new SqlParameter("@DiscoveredAt", entity.DiscoveredAt != default ? entity.DiscoveredAt : DateTimeOffset.UtcNow),
            new SqlParameter("@LastValidated", entity.LastValidated != default ? entity.LastValidated : DateTimeOffset.UtcNow),
            new SqlParameter("@ValidationMethod", entity.ValidationMethod),
            new SqlParameter("@ConfidenceScore", entity.ConfidenceScore),
            new SqlParameter("@IsSystemClassified", entity.IsSystemClassified),
            new SqlParameter("@Notes", (object?)entity.Notes ?? DBNull.Value),
            new SqlParameter("@EncryptionRequired", entity.PrivacyRequirements.EncryptionRequired),
            new SqlParameter("@MaskingRequired", entity.PrivacyRequirements.MaskingRequired),
            new SqlParameter("@RetentionDays", (object?)entity.PrivacyRequirements.RetentionDays ?? DBNull.Value),
            new SqlParameter("@AllowedCountries", (object?)allowedCountries ?? DBNull.Value),
            new SqlParameter("@RestrictedCountries", (object?)restrictedCountries ?? DBNull.Value),
            new SqlParameter("@RequiresConsent", entity.PrivacyRequirements.RequiresConsent),
            new SqlParameter("@ConsentPurposes", (object?)consentPurposes ?? DBNull.Value)
        };
    }

    protected override SqlParameter[] GetUpdateParameters(DataClassification entity)
    {
        var parameters = GetInsertParameters(entity).ToList();
        parameters.Insert(0, new SqlParameter("@Id", entity.Id));
        return parameters.ToArray();
    }
}