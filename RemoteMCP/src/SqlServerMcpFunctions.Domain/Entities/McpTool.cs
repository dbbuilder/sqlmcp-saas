using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SqlServerMcpFunctions.Domain.Entities
{
    /// <summary>
    /// Represents an MCP tool that can be executed
    /// </summary>
    public class McpTool
    {
        /// <summary>
        /// Unique identifier for the tool
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what the tool does
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON schema defining the input parameters for the tool
        /// </summary>
        public JsonDocument InputSchema { get; set; } = JsonDocument.Parse("{}");

        /// <summary>
        /// The stored procedure name that implements this tool
        /// </summary>
        public string StoredProcedureName { get; set; } = string.Empty;

        /// <summary>
        /// Database connection identifier
        /// </summary>
        public string DatabaseIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Whether the tool requires authentication
        /// </summary>
        public bool RequiresAuthentication { get; set; } = true;

        /// <summary>
        /// Required roles or permissions to execute this tool
        /// </summary>
        public List<string> RequiredRoles { get; set; } = new();

        /// <summary>
        /// Maximum execution timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether the tool modifies data (affects caching and transactions)
        /// </summary>
        public bool IsReadOnly { get; set; } = true;
    }
}
