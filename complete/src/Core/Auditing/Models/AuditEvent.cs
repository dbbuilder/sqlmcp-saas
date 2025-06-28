using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SqlMcp.Core.Auditing.Models
{
    /// <summary>
    /// Base implementation of an audit event
    /// </summary>
    public class AuditEvent : IAuditEvent
    {
        private static readonly string _machineName = Environment.MachineName;
        private static readonly string _applicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "SqlMcp";

        /// <inheritdoc/>
        public string EventId { get; protected set; }

        /// <inheritdoc/>
        public string EventType { get; protected set; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; protected set; }

        /// <inheritdoc/>
        public string UserId { get; protected set; }

        /// <inheritdoc/>
        public string CorrelationId { get; protected set; }

        /// <inheritdoc/>
        public string SessionId { get; set; }

        /// <inheritdoc/>
        public string IpAddress { get; set; }

        /// <inheritdoc/>
        public string MachineName { get; protected set; }

        /// <inheritdoc/>
        public string ApplicationName { get; protected set; }

        /// <inheritdoc/>
        public AuditSeverity Severity { get; set; } = AuditSeverity.Information;

        /// <inheritdoc/>
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Creates a new audit event
        /// </summary>
        /// <param name="eventType">Type of the event</param>
        /// <param name="userId">User ID (null defaults to "SYSTEM")</param>
        /// <param name="correlationId">Correlation ID (null generates new)</param>
        public AuditEvent(string eventType, string userId = null, string correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));
            }

            EventId = Guid.NewGuid().ToString();
            EventType = eventType;
            Timestamp = DateTimeOffset.UtcNow;
            UserId = string.IsNullOrWhiteSpace(userId) ? "SYSTEM" : userId;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString() : correlationId;
            MachineName = _machineName;
            ApplicationName = _applicationName;
            AdditionalData = new Dictionary<string, object>();
        }

        /// <summary>
        /// Protected constructor for deserialization
        /// </summary>
        protected AuditEvent()
        {
            AdditionalData = new Dictionary<string, object>();
        }

        /// <inheritdoc/>
        public virtual string ToLogString()
        {
            var parts = new List<string>
            {
                $"EventType={EventType}",
                $"EventId={EventId}",
                $"UserId={UserId}",
                $"CorrelationId={CorrelationId}",
                $"Timestamp={Timestamp:O}",
                $"Severity={Severity}"
            };

            if (!string.IsNullOrEmpty(SessionId))
            {
                parts.Add($"SessionId={SessionId}");
            }

            if (!string.IsNullOrEmpty(IpAddress))
            {
                parts.Add($"IpAddress={IpAddress}");
            }

            if (AdditionalData?.Any() == true)
            {
                try
                {
                    var additionalJson = JsonSerializer.Serialize(AdditionalData);
                    parts.Add($"AdditionalData={additionalJson}");
                }
                catch
                {
                    // If serialization fails, just note that there's additional data
                    parts.Add($"AdditionalData=[{AdditionalData.Count} items]");
                }
            }

            return string.Join(", ", parts);
        }

        /// <inheritdoc/>
        public virtual IAuditEvent Clone()
        {
            var clone = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(), // New event ID for the clone
                EventType = EventType,
                Timestamp = Timestamp,
                UserId = UserId,
                CorrelationId = CorrelationId,
                SessionId = SessionId,
                IpAddress = IpAddress,
                MachineName = MachineName,
                ApplicationName = ApplicationName,
                Severity = Severity
            };

            // Deep copy additional data
            if (AdditionalData != null)
            {
                clone.AdditionalData = new Dictionary<string, object>(AdditionalData);
            }

            return clone;
        }

        /// <summary>
        /// Add a key-value pair to additional data
        /// </summary>
        public void AddData(string key, object value)
        {
            if (AdditionalData == null)
            {
                AdditionalData = new Dictionary<string, object>();
            }

            AdditionalData[key] = value;
        }

        /// <summary>
        /// Add multiple key-value pairs to additional data
        /// </summary>
        public void AddData(IDictionary<string, object> data)
        {
            if (data == null) return;

            if (AdditionalData == null)
            {
                AdditionalData = new Dictionary<string, object>();
            }

            foreach (var kvp in data)
            {
                AdditionalData[kvp.Key] = kvp.Value;
            }
        }
    }
}
