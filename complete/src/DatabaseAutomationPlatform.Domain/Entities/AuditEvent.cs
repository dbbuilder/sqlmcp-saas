using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents an audit event for compliance and security tracking
    /// </summary>
    public class AuditEvent
    {
        /// <summary>
        /// Unique identifier for the audit event
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Type of event that occurred
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the event
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// When the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID associated with the event
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the user
        /// </summary>
        public string? UserDisplayName { get; set; }

        /// <summary>
        /// IP address where the event originated
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent or client information
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Session ID for tracking user sessions
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Correlation ID for tracking across services
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Additional event-specific data
        /// </summary>
        public Dictionary<string, object> EventData { get; set; } = new();

        /// <summary>
        /// Security context when the event occurred
        /// </summary>
        public string? SecurityContext { get; set; }

        /// <summary>
        /// Resource that was accessed or modified
        /// </summary>
        public string? ResourceId { get; set; }

        /// <summary>
        /// Type of resource (database, table, procedure, etc.)
        /// </summary>
        public string? ResourceType { get; set; }

        /// <summary>
        /// Action performed on the resource
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Result of the action (success, failure, etc.)
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// Severity level of the audit event
        /// </summary>
        public AuditSeverity Severity { get; set; } = AuditSeverity.Information;

        /// <summary>
        /// Category of the audit event
        /// </summary>
        public AuditCategory Category { get; set; } = AuditCategory.General;

        /// <summary>
        /// Whether this event should be retained for compliance
        /// </summary>
        public bool IsComplianceEvent { get; set; } = false;

        /// <summary>
        /// Retention period for this audit event
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; }

        /// <summary>
        /// Tags for categorizing and searching audit events
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Severity levels for audit events
    /// </summary>
    public enum AuditSeverity
    {
        /// <summary>
        /// Informational event
        /// </summary>
        Information,

        /// <summary>
        /// Warning - potential issue
        /// </summary>
        Warning,

        /// <summary>
        /// Error - something went wrong
        /// </summary>
        Error,

        /// <summary>
        /// Critical - security or compliance issue
        /// </summary>
        Critical
    }

    /// <summary>
    /// Categories for audit events
    /// </summary>
    public enum AuditCategory
    {
        /// <summary>
        /// General system events
        /// </summary>
        General,

        /// <summary>
        /// Authentication and authorization events
        /// </summary>
        Security,

        /// <summary>
        /// Data access and modification events
        /// </summary>
        DataAccess,

        /// <summary>
        /// Administrative operations
        /// </summary>
        Administration,

        /// <summary>
        /// Configuration changes
        /// </summary>
        Configuration,

        /// <summary>
        /// Performance and monitoring events
        /// </summary>
        Performance,

        /// <summary>
        /// Compliance-related events
        /// </summary>
        Compliance
    }
}
