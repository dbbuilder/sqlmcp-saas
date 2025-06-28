using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents an approval for a database task
    /// </summary>
    public class TaskApproval
    {
        /// <summary>
        /// Unique identifier for the approval
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// ID of the user who provided the approval
        /// </summary>
        public string ApproverId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the approver
        /// </summary>
        public string ApproverDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Role or title of the approver
        /// </summary>
        public string ApproverRole { get; set; } = string.Empty;

        /// <summary>
        /// Approval decision
        /// </summary>
        public ApprovalDecision Decision { get; set; }

        /// <summary>
        /// Comments from the approver
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// When the approval was given
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Approval level (for multi-level approvals)
        /// </summary>
        public int ApprovalLevel { get; set; } = 1;

        /// <summary>
        /// Whether this approval is still valid
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Expiration time for the approval
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Approval decision options
    /// </summary>
    public enum ApprovalDecision
    {
        /// <summary>
        /// Approval is still pending
        /// </summary>
        Pending,

        /// <summary>
        /// Task has been approved
        /// </summary>
        Approved,

        /// <summary>
        /// Task has been rejected
        /// </summary>
        Rejected,

        /// <summary>
        /// Approval has been escalated to higher authority
        /// </summary>
        Escalated
    }
}
