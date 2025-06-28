using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents an MCP resource that can be discovered and accessed
    /// </summary>
    public class McpResource
    {
        /// <summary>
        /// Unique URI identifier for the resource
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name for the resource
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the resource
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// MIME type of the resource content
        /// </summary>
        public string MimeType { get; set; } = "application/json";

        /// <summary>
        /// Additional metadata about the resource
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// When the resource was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the resource was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the resource is currently available
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Size of the resource content in bytes
        /// </summary>
        public long? ContentSize { get; set; }

        /// <summary>
        /// Tags for categorizing and searching resources
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Access level required to read this resource
        /// </summary>
        public string? RequiredAccessLevel { get; set; }

        /// <summary>
        /// Whether the resource content changes dynamically
        /// </summary>
        public bool IsDynamic { get; set; } = false;

        /// <summary>
        /// Cache duration for static resources
        /// </summary>
        public TimeSpan? CacheDuration { get; set; }
    }
}
