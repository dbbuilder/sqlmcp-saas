using System;
using System.Collections.Generic;
using System.Linq;
using SqlMcp.Core.Auditing.Models;

namespace SqlMcp.Core.Auditing.Configuration
{
    /// <summary>
    /// Configuration for the audit system
    /// </summary>
    public class AuditConfiguration
    {
        /// <summary>
        /// Whether auditing is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Global audit level
        /// </summary>
        public AuditLevel Level { get; set; } = AuditLevel.Detailed;

        /// <summary>
        /// Size of the in-memory buffer before flushing
        /// </summary>
        public int BufferSize { get; set; } = 1000;

        /// <summary>
        /// Interval in seconds between automatic flushes
        /// </summary>
        public int FlushIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Number of days to retain audit logs
        /// </summary>
        public int RetentionDays { get; set; } = 90;

        /// <summary>
        /// Connection string for audit storage
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Name of the audit database
        /// </summary>
        public string DatabaseName { get; set; } = "AuditDB";

        /// <summary>
        /// Schema for audit tables
        /// </summary>
        public string SchemaName { get; set; } = "audit";

        /// <summary>
        /// Whether to enable circuit breaker for audit writes
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Number of failures before circuit opens
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Timeout in seconds before circuit resets
        /// </summary>
        public int CircuitBreakerTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Entity-specific audit configurations
        /// </summary>
        public Dictionary<string, EntityAuditConfiguration> EntityConfigurations { get; set; } 
            = new Dictionary<string, EntityAuditConfiguration>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Operations to exclude from auditing
        /// </summary>
        public HashSet<string> ExcludedOperations { get; set; } 
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sampling rates for different operations (0.0 to 1.0)
        /// </summary>
        public Dictionary<DatabaseOperation, double> SamplingRates { get; set; }
            = new Dictionary<DatabaseOperation, double>();

        /// <summary>
        /// Azure Table Storage connection string for archival
        /// </summary>
        public string ArchiveStorageConnectionString { get; set; }

        /// <summary>
        /// Days before moving to archive storage
        /// </summary>
        public int ArchiveAfterDays { get; set; } = 30;

        /// <summary>
        /// Whether to enable performance metrics collection
        /// </summary>
        public bool CollectPerformanceMetrics { get; set; } = true;

        /// <summary>
        /// Whether to mask PII in audit logs
        /// </summary>
        public bool MaskPii { get; set; } = true;

        /// <summary>
        /// Validate the configuration
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (BufferSize <= 0 || BufferSize > 100000)
                errors.Add("Buffer size must be between 1 and 100,000");

            if (FlushIntervalSeconds <= 0 || FlushIntervalSeconds > 3600)
                errors.Add("Flush interval must be between 1 and 3600 seconds");

            if (RetentionDays <= 0 || RetentionDays > 36500)
                errors.Add("Retention days must be between 1 and 36500");

            if (CircuitBreakerThreshold <= 0)
                errors.Add("Circuit breaker threshold must be greater than 0");

            if (CircuitBreakerTimeoutSeconds <= 0)
                errors.Add("Circuit breaker timeout must be greater than 0");

            if (ArchiveAfterDays < 0 || ArchiveAfterDays > RetentionDays)
                errors.Add("Archive after days must be between 0 and retention days");

            // Validate sampling rates
            foreach (var rate in SamplingRates.Values)
            {
                if (rate < 0.0 || rate > 1.0)
                {
                    errors.Add("Sampling rates must be between 0.0 and 1.0");
                    break;
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Get effective audit level for an entity
        /// </summary>
        public AuditLevel GetEffectiveLevel(string entityName)
        {
            if (EntityConfigurations.TryGetValue(entityName, out var config))
                return config.Level;

            return Level;
        }

        /// <summary>
        /// Check if an operation is excluded
        /// </summary>
        public bool IsOperationExcluded(string operation)
        {
            return ExcludedOperations.Contains(operation);
        }

        /// <summary>
        /// Get sampling rate for an operation
        /// </summary>
        public double GetSamplingRate(DatabaseOperation operation)
        {
            return SamplingRates.TryGetValue(operation, out var rate) ? rate : 1.0;
        }

        /// <summary>
        /// Create a deep copy of the configuration
        /// </summary>
        public AuditConfiguration Clone()
        {
            var clone = new AuditConfiguration
            {
                Enabled = Enabled,
                Level = Level,
                BufferSize = BufferSize,
                FlushIntervalSeconds = FlushIntervalSeconds,
                RetentionDays = RetentionDays,
                StorageConnectionString = StorageConnectionString,
                DatabaseName = DatabaseName,
                SchemaName = SchemaName,
                EnableCircuitBreaker = EnableCircuitBreaker,
                CircuitBreakerThreshold = CircuitBreakerThreshold,
                CircuitBreakerTimeoutSeconds = CircuitBreakerTimeoutSeconds,
                ArchiveStorageConnectionString = ArchiveStorageConnectionString,
                ArchiveAfterDays = ArchiveAfterDays,
                CollectPerformanceMetrics = CollectPerformanceMetrics,
                MaskPii = MaskPii
            };

            // Deep copy collections
            clone.EntityConfigurations = new Dictionary<string, EntityAuditConfiguration>(
                EntityConfigurations.Select(kvp => 
                    new KeyValuePair<string, EntityAuditConfiguration>(
                        kvp.Key, 
                        kvp.Value.Clone())),
                StringComparer.OrdinalIgnoreCase);

            clone.ExcludedOperations = new HashSet<string>(
                ExcludedOperations, 
                StringComparer.OrdinalIgnoreCase);

            clone.SamplingRates = new Dictionary<DatabaseOperation, double>(SamplingRates);

            return clone;
        }
    }

    /// <summary>
    /// Entity-specific audit configuration
    /// </summary>
    public class EntityAuditConfiguration
    {
        /// <summary>
        /// Entity name this configuration applies to
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Audit level for this entity
        /// </summary>
        public AuditLevel Level { get; set; }

        /// <summary>
        /// Whether to include data in audit logs
        /// </summary>
        public bool IncludeData { get; set; } = true;

        /// <summary>
        /// Fields to exclude from audit
        /// </summary>
        public HashSet<string> ExcludedFields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fields that contain PII
        /// </summary>
        public HashSet<string> PiiFields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create a deep copy
        /// </summary>
        public EntityAuditConfiguration Clone()
        {
            return new EntityAuditConfiguration
            {
                EntityName = EntityName,
                Level = Level,
                IncludeData = IncludeData,
                ExcludedFields = new HashSet<string>(ExcludedFields, StringComparer.OrdinalIgnoreCase),
                PiiFields = new HashSet<string>(PiiFields, StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
