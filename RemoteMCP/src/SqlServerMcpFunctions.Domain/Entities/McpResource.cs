using System;
using System.Collections.Generic;

namespace SqlServerMcpFunctions.Domain.Entities
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
    }
}
