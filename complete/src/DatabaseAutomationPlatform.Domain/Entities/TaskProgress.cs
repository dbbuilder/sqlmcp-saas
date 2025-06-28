using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents progress information for a long-running database task
    /// </summary>
    public class TaskProgress
    {
        /// <summary>
        /// Unique identifier for this progress update
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Percentage complete (0-100)
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Current step or operation being performed
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable progress message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When this progress update was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional details about the current progress
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();

        /// <summary>
        /// Estimated time remaining for completion
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Number of items processed so far
        /// </summary>
        public long? ItemsProcessed { get; set; }

        /// <summary>
        /// Total number of items to process
        /// </summary>
        public long? TotalItems { get; set; }

        /// <summary>
        /// Current processing rate (items per second)
        /// </summary>
        public double? ProcessingRate { get; set; }

        /// <summary>
        /// Any warnings or issues encountered during this step
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Severity level of this progress update
        /// </summary>
        public ProgressSeverity Severity { get; set; } = ProgressSeverity.Information;
    }

    /// <summary>
    /// Severity levels for progress updates
    /// </summary>
    public enum ProgressSeverity
    {
        /// <summary>
        /// Informational progress update
        /// </summary>
        Information,

        /// <summary>
        /// Warning - task is proceeding but with issues
        /// </summary>
        Warning,

        /// <summary>
        /// Error - significant problem encountered
        /// </summary>
        Error,

        /// <summary>
        /// Critical - task may fail or cause issues
        /// </summary>
        Critical
    }
}
