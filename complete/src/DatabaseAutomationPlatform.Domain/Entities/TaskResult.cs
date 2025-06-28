using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents the result of a database task execution
    /// </summary>
    public class TaskResult
    {
        /// <summary>
        /// Whether the task execution was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Result data from the task execution
        /// </summary>
        public object? Data { get; set; }

        /// <summary>
        /// Messages generated during task execution
        /// </summary>
        public List<string> Messages { get; set; } = new();

        /// <summary>
        /// Performance and execution metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// Total execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Number of database rows affected (for modification operations)
        /// </summary>
        public int RowsAffected { get; set; }

        /// <summary>
        /// Location where large result data is stored (e.g., blob storage URL)
        /// </summary>
        public string? OutputLocation { get; set; }

        /// <summary>
        /// MIME type of the result data
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Size of the result data in bytes
        /// </summary>
        public long? DataSize { get; set; }

        /// <summary>
        /// Checksum or hash of the result data for integrity verification
        /// </summary>
        public string? DataChecksum { get; set; }

        /// <summary>
        /// Additional metadata about the result
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Any warnings generated during execution
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Performance recommendations based on execution
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }
}
