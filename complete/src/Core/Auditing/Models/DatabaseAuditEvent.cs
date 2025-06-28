using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlMcp.Core.Auditing.Models
{
    /// <summary>
    /// Database operations that can be audited
    /// </summary>
    public enum DatabaseOperation
    {
        /// <summary>
        /// Create/Insert operation
        /// </summary>
        Create,

        /// <summary>
        /// Read/Select operation
        /// </summary>
        Read,

        /// <summary>
        /// Update operation
        /// </summary>
        Update,

        /// <summary>
        /// Delete operation
        /// </summary>
        Delete,

        /// <summary>
        /// Execute stored procedure
        /// </summary>
        Execute,

        /// <summary>
        /// Insert operation (alias for Create)
        /// </summary>
        Insert = Create,

        /// <summary>
        /// Select operation (alias for Read)
        /// </summary>
        Select = Read
    }

    /// <summary>
    /// Represents a changed field in a database operation
    /// </summary>
    public class FieldChange
    {
        /// <summary>
        /// Name of the field that changed
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Original value before the change
        /// </summary>
        public object OldValue { get; set; }

        /// <summary>
        /// New value after the change
        /// </summary>
        public object NewValue { get; set; }

        /// <summary>
        /// Indicates if the field contains PII
        /// </summary>
        public bool IsPii { get; set; }
    }

    /// <summary>
    /// Audit event for database operations
    /// </summary>
    public class DatabaseAuditEvent : AuditEvent
    {
        /// <summary>
        /// The database operation performed
        /// </summary>
        public DatabaseOperation Operation { get; set; }

        /// <summary>
        /// Name of the entity/table affected
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// ID of the entity affected
        /// </summary>
        public string EntityId { get; set; }

        /// <summary>
        /// Name of the stored procedure (if applicable)
        /// </summary>
        public string StoredProcedureName { get; set; }

        /// <summary>
        /// Parameters passed to the operation
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Data before the operation (for updates)
        /// </summary>
        public Dictionary<string, object> BeforeData { get; set; }

        /// <summary>
        /// Data after the operation
        /// </summary>
        public Dictionary<string, object> AfterData { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public long? ExecutionTimeMs { get; set; }

        /// <summary>
        /// Number of rows affected
        /// </summary>
        public int? RowsAffected { get; set; }

        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Database name
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Schema name
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Creates a new database audit event
        /// </summary>
        public DatabaseAuditEvent(
            DatabaseOperation operation,
            string entityName,
            string entityId,
            string userId = null,
            string correlationId = null)
            : base($"Database.{operation}", userId, correlationId)
        {
            Operation = operation;
            EntityName = entityName;
            EntityId = entityId;

            // Set severity based on operation
            Severity = operation switch
            {
                DatabaseOperation.Delete => AuditSeverity.Warning,
                DatabaseOperation.Create => AuditSeverity.Information,
                DatabaseOperation.Update => AuditSeverity.Information,
                DatabaseOperation.Read => AuditSeverity.Verbose,
                DatabaseOperation.Execute => AuditSeverity.Information,
                _ => AuditSeverity.Information
            };

            // Update severity if operation fails
            if (!Success && !string.IsNullOrEmpty(ErrorMessage))
            {
                Severity = AuditSeverity.Error;
            }
        }

        /// <summary>
        /// Protected constructor for deserialization
        /// </summary>
        protected DatabaseAuditEvent() : base()
        {
        }

        /// <summary>
        /// Get the list of fields that changed
        /// </summary>
        public List<FieldChange> GetChangedFields()
        {
            var changes = new List<FieldChange>();

            if (BeforeData == null || AfterData == null)
                return changes;

            // Find all unique field names
            var allFields = BeforeData.Keys.Union(AfterData.Keys).Distinct();

            foreach (var field in allFields)
            {
                var beforeValue = BeforeData.TryGetValue(field, out var bv) ? bv : null;
                var afterValue = AfterData.TryGetValue(field, out var av) ? av : null;

                // Check if values are different
                if (!Equals(beforeValue, afterValue))
                {
                    changes.Add(new FieldChange
                    {
                        FieldName = field,
                        OldValue = beforeValue,
                        NewValue = afterValue
                    });
                }
            }

            return changes;
        }

        /// <inheritdoc/>
        public override string ToLogString()
        {
            var sb = new StringBuilder(base.ToLogString());

            sb.Append($", Operation={Operation}");
            sb.Append($", Entity={EntityName}");
            
            if (!string.IsNullOrEmpty(EntityId))
                sb.Append($", EntityId={EntityId}");

            if (!string.IsNullOrEmpty(StoredProcedureName))
                sb.Append($", StoredProcedure={StoredProcedureName}");

            if (ExecutionTimeMs.HasValue)
                sb.Append($", ExecutionTime={ExecutionTimeMs}ms");

            if (RowsAffected.HasValue)
                sb.Append($", RowsAffected={RowsAffected}");

            if (!Success)
                sb.Append($", Success=false");

            if (!string.IsNullOrEmpty(ErrorMessage))
                sb.Append($", Error={ErrorMessage}");

            var changedFields = GetChangedFields();
            if (changedFields.Any())
                sb.Append($", ChangedFields={string.Join(",", changedFields.Select(f => f.FieldName))}");

            return sb.ToString();
        }

        /// <inheritdoc/>
        public override IAuditEvent Clone()
        {
            var clone = new DatabaseAuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = EventType,
                Timestamp = Timestamp,
                UserId = UserId,
                CorrelationId = CorrelationId,
                SessionId = SessionId,
                IpAddress = IpAddress,
                MachineName = MachineName,
                ApplicationName = ApplicationName,
                Severity = Severity,
                Operation = Operation,
                EntityName = EntityName,
                EntityId = EntityId,
                StoredProcedureName = StoredProcedureName,
                ExecutionTimeMs = ExecutionTimeMs,
                RowsAffected = RowsAffected,
                Success = Success,
                ErrorMessage = ErrorMessage,
                DatabaseName = DatabaseName,
                SchemaName = SchemaName
            };

            // Deep copy dictionaries
            if (AdditionalData != null)
                clone.AdditionalData = new Dictionary<string, object>(AdditionalData);

            if (Parameters != null)
                clone.Parameters = new Dictionary<string, object>(Parameters);

            if (BeforeData != null)
                clone.BeforeData = new Dictionary<string, object>(BeforeData);

            if (AfterData != null)
                clone.AfterData = new Dictionary<string, object>(AfterData);

            return clone;
        }
    }
}
