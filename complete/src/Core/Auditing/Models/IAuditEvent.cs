using System;
using System.Collections.Generic;

namespace SqlMcp.Core.Auditing.Models
{
    /// <summary>
    /// Base interface for all audit events in the system
    /// </summary>
    public interface IAuditEvent
    {
        /// <summary>
        /// Unique identifier for this audit event
        /// </summary>
        string EventId { get; }

        /// <summary>
        /// Type of the audit event (e.g., "UserLogin", "DataUpdate", "SecurityAlert")
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// UTC timestamp when the event occurred
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// User ID associated with the event (or "SYSTEM" for system events)
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Correlation ID to track events across a request/transaction
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// Session ID for the user session (if applicable)
        /// </summary>
        string SessionId { get; set; }

        /// <summary>
        /// IP address of the client (if applicable)
        /// </summary>
        string IpAddress { get; set; }

        /// <summary>
        /// Name of the machine where the event originated
        /// </summary>
        string MachineName { get; }

        /// <summary>
        /// Name of the application generating the event
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Severity level of the audit event
        /// </summary>
        AuditSeverity Severity { get; set; }

        /// <summary>
        /// Additional context data for the event
        /// </summary>
        Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Convert the event to a loggable string representation
        /// </summary>
        string ToLogString();

        /// <summary>
        /// Create a deep copy of the audit event
        /// </summary>
        IAuditEvent Clone();
    }

    /// <summary>
    /// Severity levels for audit events
    /// </summary>
    public enum AuditSeverity
    {
        /// <summary>
        /// Verbose/debug level events
        /// </summary>
        Verbose = 0,

        /// <summary>
        /// Informational events (default)
        /// </summary>
        Information = 1,

        /// <summary>
        /// Warning events that may require attention
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Error events indicating failures
        /// </summary>
        Error = 3,

        /// <summary>
        /// Critical security or system events
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Audit levels for configuring what gets audited
    /// </summary>
    public enum AuditLevel
    {
        /// <summary>
        /// No auditing
        /// </summary>
        None = 0,

        /// <summary>
        /// Only critical security events
        /// </summary>
        Critical = 1,

        /// <summary>
        /// Basic CRUD operations without data
        /// </summary>
        Basic = 2,

        /// <summary>
        /// CRUD operations with before/after values
        /// </summary>
        Detailed = 3,

        /// <summary>
        /// All operations including reads
        /// </summary>
        Verbose = 4
    }
}
