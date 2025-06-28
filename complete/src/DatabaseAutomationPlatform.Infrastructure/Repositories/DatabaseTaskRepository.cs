using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for database tasks
/// </summary>
public class DatabaseTaskRepository : BaseRepository<DatabaseTask, Guid>, IDatabaseTaskRepository
{
    public DatabaseTaskRepository(IStoredProcedureExecutor executor, ILogger<DatabaseTaskRepository> logger)
        : base(executor, logger, "DatabaseTask")
    {
    }

    public async Task<IEnumerable<DatabaseTask>> GetByStatusAsync(Domain.Entities.TaskStatus status, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting database tasks by status: {Status}", status);

        var parameters = new[]
        {
            new SqlParameter("@Status", status.ToString())
        };

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetByStatus", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DatabaseTask>> GetByTypeAsync(TaskType type, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting database tasks by type: {Type}", type);

        var parameters = new[]
        {
            new SqlParameter("@Type", type.ToString())
        };

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetByType", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DatabaseTask>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting database tasks pending approval");

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetPendingApproval", Array.Empty<SqlParameter>(), cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DatabaseTask>> GetByDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting database tasks for database: {DatabaseName}", databaseName);

        var parameters = new[]
        {
            new SqlParameter("@DatabaseName", databaseName)
        };

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetByDatabase", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DatabaseTask>> GetByCreatedByAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting database tasks created by user: {UserId}", userId);

        var parameters = new[]
        {
            new SqlParameter("@CreatedBy", userId)
        };

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetByCreatedBy", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<DatabaseTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting active database tasks");

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_GetActive", Array.Empty<SqlParameter>(), cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<bool> UpdateStatusAsync(Guid taskId, Domain.Entities.TaskStatus newStatus, string? message = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating task {TaskId} status to {NewStatus}", taskId, newStatus);

        var parameters = new[]
        {
            new SqlParameter("@TaskId", taskId),
            new SqlParameter("@NewStatus", newStatus.ToString()),
            new SqlParameter("@Message", (object?)message ?? DBNull.Value),
            new SqlParameter("@UpdatedAt", DateTimeOffset.UtcNow),
            new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output }
        };

        await _executor.ExecuteNonQueryAsync("sp_DatabaseTask_UpdateStatus", parameters, cancellationToken);
        
        var success = (bool)parameters[4].Value;
        
        if (success)
        {
            _logger.LogInformation("Task {TaskId} status updated successfully", taskId);
        }
        else
        {
            _logger.LogWarning("Failed to update task {TaskId} status", taskId);
        }

        return success;
    }

    public async Task AddProgressAsync(Guid taskId, TaskProgress progress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding progress to task {TaskId}: {Progress}", taskId, progress.Message);

        var parameters = new[]
        {
            new SqlParameter("@TaskId", taskId),
            new SqlParameter("@Timestamp", progress.Timestamp),
            new SqlParameter("@Message", progress.Message),
            new SqlParameter("@PercentComplete", (object?)progress.PercentComplete ?? DBNull.Value),
            new SqlParameter("@Details", (object?)progress.Details ?? DBNull.Value)
        };

        await _executor.ExecuteNonQueryAsync("sp_TaskProgress_Insert", parameters, cancellationToken);
    }

    public async Task AddResultAsync(Guid taskId, TaskResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding result to task {TaskId}: Success={Success}", taskId, result.Success);

        var outputJson = result.Output != null ? JsonSerializer.Serialize(result.Output) : null;

        var parameters = new[]
        {
            new SqlParameter("@TaskId", taskId),
            new SqlParameter("@Success", result.Success),
            new SqlParameter("@Output", (object?)outputJson ?? DBNull.Value),
            new SqlParameter("@ErrorMessage", (object?)result.ErrorMessage ?? DBNull.Value),
            new SqlParameter("@ExecutionTime", result.ExecutionTime),
            new SqlParameter("@RowsAffected", (object?)result.RowsAffected ?? DBNull.Value),
            new SqlParameter("@CompletedAt", result.CompletedAt)
        };

        await _executor.ExecuteNonQueryAsync("sp_TaskResult_Insert", parameters, cancellationToken);
    }

    public async Task<PagedResult<DatabaseTask>> SearchAsync(TaskSearchCriteria criteria, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching database tasks with criteria: {@Criteria}", criteria);

        var parameters = new[]
        {
            new SqlParameter("@Status", (object?)criteria.Status?.ToString() ?? DBNull.Value),
            new SqlParameter("@Type", (object?)criteria.Type?.ToString() ?? DBNull.Value),
            new SqlParameter("@DatabaseName", (object?)criteria.DatabaseName ?? DBNull.Value),
            new SqlParameter("@CreatedBy", (object?)criteria.CreatedBy ?? DBNull.Value),
            new SqlParameter("@CreatedAfter", (object?)criteria.CreatedAfter ?? DBNull.Value),
            new SqlParameter("@CreatedBefore", (object?)criteria.CreatedBefore ?? DBNull.Value),
            new SqlParameter("@RequiresApproval", (object?)criteria.RequiresApproval ?? DBNull.Value),
            new SqlParameter("@SearchText", (object?)criteria.SearchText ?? DBNull.Value),
            new SqlParameter("@PageNumber", pageNumber),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@TotalCount", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
        };

        var result = await _executor.ExecuteAsync("sp_DatabaseTask_Search", parameters, cancellationToken);
        var items = result.Rows.Cast<DataRow>().Select(MapFromDataRow).ToList();
        var totalCount = (long)parameters[10].Value;

        return new PagedResult<DatabaseTask>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    protected override DatabaseTask MapFromDataRow(DataRow row)
    {
        var task = new DatabaseTask
        {
            Id = row.Field<Guid>("Id"),
            Type = Enum.Parse<TaskType>(row.Field<string>("Type") ?? "Query"),
            Status = Enum.Parse<Domain.Entities.TaskStatus>(row.Field<string>("Status") ?? "Created"),
            Priority = Enum.Parse<TaskPriority>(row.Field<string>("Priority") ?? "Normal"),
            DatabaseName = row.Field<string>("DatabaseName") ?? string.Empty,
            Description = row.Field<string>("Description") ?? string.Empty,
            SqlStatement = row.Field<string>("SqlStatement"),
            RequiresApproval = row.Field<bool>("RequiresApproval"),
            ApprovalStatus = row.Field<string>("ApprovalStatus") != null 
                ? Enum.Parse<ApprovalStatus>(row.Field<string>("ApprovalStatus")!)
                : null,
            CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
            CreatedAt = row.Field<DateTimeOffset>("CreatedAt"),
            UpdatedAt = row.Field<DateTimeOffset?>("UpdatedAt"),
            ScheduledFor = row.Field<DateTimeOffset?>("ScheduledFor"),
            StartedAt = row.Field<DateTimeOffset?>("StartedAt"),
            CompletedAt = row.Field<DateTimeOffset?>("CompletedAt"),
            EstimatedDuration = row.Field<TimeSpan?>("EstimatedDuration"),
            ActualDuration = row.Field<TimeSpan?>("ActualDuration"),
            RetryCount = row.Field<int>("RetryCount"),
            MaxRetries = row.Field<int>("MaxRetries"),
            Tags = row.Field<string>("Tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            Metadata = row.Field<string>("Metadata") != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.Field<string>("Metadata")!)
                : new Dictionary<string, object>()
        };

        // Note: Progress, Results, and Approvals would need to be loaded separately
        // through their respective repositories or via joined queries

        return task;
    }

    protected override SqlParameter[] GetInsertParameters(DatabaseTask entity)
    {
        var tagsString = entity.Tags.Any() ? string.Join(",", entity.Tags) : null;
        var metadataJson = entity.Metadata.Any() ? JsonSerializer.Serialize(entity.Metadata) : null;

        return new[]
        {
            new SqlParameter("@Id", entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid()),
            new SqlParameter("@Type", entity.Type.ToString()),
            new SqlParameter("@Status", entity.Status.ToString()),
            new SqlParameter("@Priority", entity.Priority.ToString()),
            new SqlParameter("@DatabaseName", entity.DatabaseName),
            new SqlParameter("@Description", entity.Description),
            new SqlParameter("@SqlStatement", (object?)entity.SqlStatement ?? DBNull.Value),
            new SqlParameter("@RequiresApproval", entity.RequiresApproval),
            new SqlParameter("@ApprovalStatus", (object?)entity.ApprovalStatus?.ToString() ?? DBNull.Value),
            new SqlParameter("@CreatedBy", entity.CreatedBy),
            new SqlParameter("@CreatedAt", entity.CreatedAt != default ? entity.CreatedAt : DateTimeOffset.UtcNow),
            new SqlParameter("@ScheduledFor", (object?)entity.ScheduledFor ?? DBNull.Value),
            new SqlParameter("@EstimatedDuration", (object?)entity.EstimatedDuration ?? DBNull.Value),
            new SqlParameter("@MaxRetries", entity.MaxRetries),
            new SqlParameter("@Tags", (object?)tagsString ?? DBNull.Value),
            new SqlParameter("@Metadata", (object?)metadataJson ?? DBNull.Value)
        };
    }

    protected override SqlParameter[] GetUpdateParameters(DatabaseTask entity)
    {
        var tagsString = entity.Tags.Any() ? string.Join(",", entity.Tags) : null;
        var metadataJson = entity.Metadata.Any() ? JsonSerializer.Serialize(entity.Metadata) : null;

        return new[]
        {
            new SqlParameter("@Id", entity.Id),
            new SqlParameter("@Type", entity.Type.ToString()),
            new SqlParameter("@Status", entity.Status.ToString()),
            new SqlParameter("@Priority", entity.Priority.ToString()),
            new SqlParameter("@DatabaseName", entity.DatabaseName),
            new SqlParameter("@Description", entity.Description),
            new SqlParameter("@SqlStatement", (object?)entity.SqlStatement ?? DBNull.Value),
            new SqlParameter("@RequiresApproval", entity.RequiresApproval),
            new SqlParameter("@ApprovalStatus", (object?)entity.ApprovalStatus?.ToString() ?? DBNull.Value),
            new SqlParameter("@UpdatedAt", DateTimeOffset.UtcNow),
            new SqlParameter("@ScheduledFor", (object?)entity.ScheduledFor ?? DBNull.Value),
            new SqlParameter("@StartedAt", (object?)entity.StartedAt ?? DBNull.Value),
            new SqlParameter("@CompletedAt", (object?)entity.CompletedAt ?? DBNull.Value),
            new SqlParameter("@EstimatedDuration", (object?)entity.EstimatedDuration ?? DBNull.Value),
            new SqlParameter("@ActualDuration", (object?)entity.ActualDuration ?? DBNull.Value),
            new SqlParameter("@RetryCount", entity.RetryCount),
            new SqlParameter("@Tags", (object?)tagsString ?? DBNull.Value),
            new SqlParameter("@Metadata", (object?)metadataJson ?? DBNull.Value)
        };
    }
}