using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Repositories;

/// <summary>
/// Repository interface for database task operations
/// </summary>
public interface IDatabaseTaskRepository : IRepository<DatabaseTask, Guid>
{
    /// <summary>
    /// Get tasks by status
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetByStatusAsync(TaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tasks by type
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetByTypeAsync(TaskType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tasks requiring approval
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tasks by database
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetByDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tasks created by user
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetByCreatedByAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active tasks (not completed or failed)
    /// </summary>
    Task<IEnumerable<DatabaseTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update task status
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid taskId, TaskStatus newStatus, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add task progress
    /// </summary>
    Task AddProgressAsync(Guid taskId, TaskProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add task result
    /// </summary>
    Task AddResultAsync(Guid taskId, TaskResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search tasks
    /// </summary>
    Task<PagedResult<DatabaseTask>> SearchAsync(TaskSearchCriteria criteria, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for database tasks
/// </summary>
public class TaskSearchCriteria
{
    public TaskStatus? Status { get; set; }
    public TaskType? Type { get; set; }
    public string? DatabaseName { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? CreatedAfter { get; set; }
    public DateTimeOffset? CreatedBefore { get; set; }
    public bool? RequiresApproval { get; set; }
    public string? SearchText { get; set; }
}