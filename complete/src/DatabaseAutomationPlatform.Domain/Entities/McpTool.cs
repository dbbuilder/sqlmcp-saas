using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents an MCP tool that can be executed by AI assistants
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
        /// The database task type that implements this tool
        /// </summary>
        public DatabaseTaskType TaskType { get; set; }

        /// <summary>
        /// Database connection identifier for execution
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
        /// Required permissions to execute this tool
        /// </summary>
        public List<string> RequiredPermissions { get; set; } = new();

        /// <summary>
        /// Maximum execution timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether the tool modifies data (affects caching and transactions)
        /// </summary>
        public bool IsReadOnly { get; set; } = true;

        /// <summary>
        /// Whether the tool can run for a long time (requires async execution)
        /// </summary>
        public bool IsLongRunning { get; set; } = false;

        /// <summary>
        /// Risk level of executing this tool
        /// </summary>
        public TaskRiskLevel RiskLevel { get; set; } = TaskRiskLevel.Low;

        /// <summary>
        /// Category of the tool for organization
        /// </summary>
        public ToolCategory Category { get; set; } = ToolCategory.Analysis;

        /// <summary>
        /// Whether approval is required before execution
        /// </summary>
        public bool RequiresApproval { get; set; } = false;

        /// <summary>
        /// Expected output format
        /// </summary>
        public string OutputFormat { get; set; } = "application/json";

        /// <summary>
        /// Example usage of the tool
        /// </summary>
        public string? ExampleUsage { get; set; }

        /// <summary>
        /// Tags for categorizing and searching tools
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// When the tool was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the tool was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the tool is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Rate limiting configuration for the tool
        /// </summary>
        public RateLimitConfiguration? RateLimit { get; set; }

        /// <summary>
        /// Additional metadata about the tool
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Categories for organizing tools
    /// </summary>
    public enum ToolCategory
    {
        /// <summary>
        /// Data analysis and profiling tools
        /// </summary>
        Analysis,

        /// <summary>
        /// Schema and structure tools
        /// </summary>
        Schema,

        /// <summary>
        /// Performance monitoring and optimization tools
        /// </summary>
        Performance,

        /// <summary>
        /// Security and compliance tools
        /// </summary>
        Security,

        /// <summary>
        /// Administrative and maintenance tools
        /// </summary>
        Administration,

        /// <summary>
        /// Development and query tools
        /// </summary>
        Development,

        /// <summary>
        /// Reporting and visualization tools
        /// </summary>
        Reporting
    }

    /// <summary>
    /// Rate limiting configuration for tools
    /// </summary>
    public class RateLimitConfiguration
    {
        /// <summary>
        /// Maximum number of executions per time period
        /// </summary>
        public int MaxExecutions { get; set; }

        /// <summary>
        /// Time period for rate limiting
        /// </summary>
        public TimeSpan TimePeriod { get; set; }

        /// <summary>
        /// Whether rate limiting is per user or global
        /// </summary>
        public bool IsPerUser { get; set; } = true;
    }
}
