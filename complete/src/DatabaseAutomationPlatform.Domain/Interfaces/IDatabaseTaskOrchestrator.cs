using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAutomationPlatform.Domain.Entities;

namespace DatabaseAutomationPlatform.Domain.Interfaces
{
    /// <summary>
    /// Interface for orchestrating complex database tasks with approval workflows and monitoring
    /// </summary>
    public interface IDatabaseTaskOrchestrator
    {
        /// <summary>
        /// Submit a new database task for execution
        /// </summary>
        /// <param name="task">Database task to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task ID for tracking</returns>
        Task<Guid> SubmitTaskAsync(DatabaseTask task, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute an approved database task
        /// </summary>
        /// <param name="taskId">Task ID to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ExecuteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get task status and progress
        /// </summary>
        /// <param name="taskId">Task ID to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task information with current status</returns>
        Task<DatabaseTask?> GetTaskStatusAsync(Guid taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel a running task
        /// </summary>
        /// <param name="taskId">Task ID to cancel</param>
        /// <param name="userId">User requesting cancellation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CancelTaskAsync(Guid taskId, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// List tasks for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="pageSize">Page size for pagination</param>
        /// <param name="pageNumber">Page number for pagination</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of user tasks</returns>
        Task<PagedResult<DatabaseTask>> GetUserTasksAsync(
            string userId, 
            TaskStatus? status = null, 
            int pageSize = 50, 
            int pageNumber = 1,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get task execution metrics and statistics
        /// </summary>
        /// <param name="fromDate">Start date for metrics</param>
        /// <param name="toDate">End date for metrics</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task execution metrics</returns>
        Task<TaskExecutionMetrics> GetExecutionMetricsAsync(
            DateTime fromDate, 
            DateTime toDate, 
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Task execution metrics
    /// </summary>
    public class TaskExecutionMetrics
    {
        public int TotalTasks { get; set; }
        public int SuccessfulTasks { get; set; }
        public int FailedTasks { get; set; }
        public int CancelledTasks { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public Dictionary<DatabaseTaskType, int> TasksByType { get; set; } = new();
        public Dictionary<TaskRiskLevel, int> TasksByRiskLevel { get; set; } = new();
        public List<string> TopUsers { get; set; } = new();
        public List<string> MostUsedDatabases { get; set; } = new();
    }
}
