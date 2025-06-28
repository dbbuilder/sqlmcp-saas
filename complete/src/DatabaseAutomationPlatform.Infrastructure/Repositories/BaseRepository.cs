using DatabaseAutomationPlatform.Domain.Interfaces;
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
/// Base repository implementation using stored procedures
/// </summary>
public abstract class BaseRepository<T, TKey> : IRepository<T, TKey> where T : class
{
    protected readonly IStoredProcedureExecutor _executor;
    protected readonly ILogger _logger;
    protected readonly string _entityName;

    protected BaseRepository(IStoredProcedureExecutor executor, ILogger logger, string entityName)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
    }

    public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting {EntityName} by ID: {Id}", _entityName, id);

        var parameters = new[]
        {
            new SqlParameter("@Id", id)
        };

        var result = await _executor.ExecuteAsync($"sp_{_entityName}_GetById", parameters, cancellationToken);
        
        if (result.Rows.Count == 0)
        {
            _logger.LogWarning("{EntityName} with ID {Id} not found", _entityName, id);
            return null;
        }

        return MapFromDataRow(result.Rows[0]);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all {EntityName} entities", _entityName);

        var result = await _executor.ExecuteAsync($"sp_{_entityName}_GetAll", Array.Empty<SqlParameter>(), cancellationToken);
        
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow).ToList();
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        _logger.LogInformation("Adding new {EntityName}", _entityName);

        var parameters = GetInsertParameters(entity);
        var result = await _executor.ExecuteAsync($"sp_{_entityName}_Insert", parameters, cancellationToken);

        if (result.Rows.Count == 0)
        {
            throw new InvalidOperationException($"Failed to insert {_entityName}");
        }

        var insertedEntity = MapFromDataRow(result.Rows[0]);
        _logger.LogInformation("{EntityName} added successfully", _entityName);
        
        return insertedEntity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        _logger.LogInformation("Updating {EntityName}", _entityName);

        var parameters = GetUpdateParameters(entity);
        var result = await _executor.ExecuteAsync($"sp_{_entityName}_Update", parameters, cancellationToken);

        if (result.Rows.Count == 0)
        {
            throw new InvalidOperationException($"Failed to update {_entityName}");
        }

        var updatedEntity = MapFromDataRow(result.Rows[0]);
        _logger.LogInformation("{EntityName} updated successfully", _entityName);
        
        return updatedEntity;
    }

    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting {EntityName} with ID: {Id}", _entityName, id);

        var parameters = new[]
        {
            new SqlParameter("@Id", id),
            new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output }
        };

        await _executor.ExecuteNonQueryAsync($"sp_{_entityName}_Delete", parameters, cancellationToken);
        
        var success = (bool)parameters[1].Value;
        
        if (success)
        {
            _logger.LogInformation("{EntityName} with ID {Id} deleted successfully", _entityName, id);
        }
        else
        {
            _logger.LogWarning("{EntityName} with ID {Id} not found for deletion", _entityName, id);
        }

        return success;
    }

    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if {EntityName} exists with ID: {Id}", _entityName, id);

        var parameters = new[]
        {
            new SqlParameter("@Id", id)
        };

        return await _executor.ExecuteScalarAsync<bool>($"sp_{_entityName}_Exists", parameters, cancellationToken);
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        _logger.LogInformation("Getting paged {EntityName} - Page: {PageNumber}, Size: {PageSize}", 
            _entityName, pageNumber, pageSize);

        var parameters = new[]
        {
            new SqlParameter("@PageNumber", pageNumber),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@TotalCount", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
        };

        var result = await _executor.ExecuteAsync($"sp_{_entityName}_GetPaged", parameters, cancellationToken);
        
        var items = result.Rows.Cast<DataRow>().Select(MapFromDataRow).ToList();
        var totalCount = (long)parameters[2].Value;

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Map a data row to entity
    /// </summary>
    protected abstract T MapFromDataRow(DataRow row);

    /// <summary>
    /// Get parameters for insert stored procedure
    /// </summary>
    protected abstract SqlParameter[] GetInsertParameters(T entity);

    /// <summary>
    /// Get parameters for update stored procedure
    /// </summary>
    protected abstract SqlParameter[] GetUpdateParameters(T entity);
}