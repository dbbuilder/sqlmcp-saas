using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents a database automation task that can be long-running and require approval
    /// </summary>
    public class DatabaseTask
    {
        /// <summary>
        /// Unique identifier for the task
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Human-readable name for the task
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what the task will do
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Type of database task being performed
        /// </summary>
        public DatabaseTaskType TaskType { get; set; }

        /// <summary>
        /// Risk level assessment for this task
        /// </summary>
        public TaskRiskLevel RiskLevel { get; set; }

        /// <summary>
        /// Current status of the task
        /// </summary>
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        // User and security context
        /// <summary>
        /// User ID who submitted the task
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the user who submitted the task
        /// </summary>
        public string UserDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Required roles to execute this task
        /// </summary>
        public List<string> RequiredRoles { get; set; } = new();

        /// <summary>
        /// Required permissions to execute this task
        /// </summary>
        public List<string> RequiredPermissions { get; set; } = new();

        // Task execution details
        /// <summary>
        /// Database identifier where the task will be executed
        /// </summary>
        public string DatabaseIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Parameters for task execution
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// When the task was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the task execution started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the task execution completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Estimated duration for task completion
        /// </summary>
        public TimeSpan? EstimatedDuration { get; set; }

        // Approval workflow
        /// <summary>
        /// Current approval status
        /// </summary>
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

        /// <summary>
        /// List of approvals for this task
        /// </summary>
        public List<TaskApproval> Approvals { get; set; } = new();

        /// <summary>
        /// Justification for why this task needs to be executed
        /// </summary>
        public string? ApprovalJustification { get; set; }

        // Results and monitoring
        /// <summary>
        /// Task execution result
        /// </summary>
        public TaskResult? Result { get; set; }

        /// <summary>
        /// Progress updates during task execution
        /// </summary>
        public List<TaskProgress> ProgressUpdates { get; set; } = new();

        /// <summary>
        /// Error message if task failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional metadata about the task
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        // Audit and compliance
        /// <summary>
        /// Correlation ID for tracking across services
        /// </summary>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Audit events associated with this task
        /// </summary>
        public List<AuditEvent> AuditEvents { get; set; } = new();

        /// <summary>
        /// Compliance reason for executing this task
        /// </summary>
        public string? ComplianceReason { get; set; }

        /// <summary>
        /// Whether this task requires comprehensive audit logging
        /// </summary>
        public bool RequiresAuditLog { get; set; } = true;
    }
}
