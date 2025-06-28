using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlServerMcpFunctions.Domain.Entities;
using SqlServerMcpFunctions.Domain.ValueObjects;

namespace SqlServerMcpFunctions.Domain.Interfaces
{
    /// <summary>
    /// Core MCP server interface
    /// </summary>
    public interface IMcpServer
    {
        /// <summary>
        /// Initialize the MCP server
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get server capabilities
        /// </summary>
        /// <returns>Server capabilities information</returns>
        Task<McpServerCapabilities> GetCapabilitiesAsync();

        /// <summary>
        /// List all available resources
        /// </summary>
        /// <param name="cursor">Pagination cursor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available resources</returns>
        Task<McpResourceList> ListResourcesAsync(string? cursor = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific resource by URI
        /// </summary>
        /// <param name="uri">Resource URI</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Resource content</returns>
        Task<McpResourceContent> GetResourceAsync(string uri, CancellationToken cancellationToken = default);

        /// <summary>
        /// List all available tools
        /// </summary>
        /// <param name="cursor">Pagination cursor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available tools</returns>
        Task<McpToolList> ListToolsAsync(string? cursor = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a tool with given parameters
        /// </summary>
        /// <param name="toolName">Name of the tool to execute</param>
        /// <param name="parameters">Tool parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Tool execution result</returns>
        Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object?> parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handle an MCP request message
        /// </summary>
        /// <param name="request">MCP request message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>MCP response message</returns>
        Task<McpMessage> HandleRequestAsync(McpMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Shutdown the MCP server gracefully
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// MCP server capabilities
    /// </summary>
    public class McpServerCapabilities
    {
        public bool SupportsResources { get; set; } = true;
        public bool SupportsTools { get; set; } = true;
        public bool SupportsPrompts { get; set; } = false;
        public bool SupportsLogging { get; set; } = true;
        public string ServerVersion { get; set; } = "1.0.0";
        public string ProtocolVersion { get; set; } = "2024-11-05";
    }

    /// <summary>
    /// List of MCP resources
    /// </summary>
    public class McpResourceList
    {
        public List<McpResource> Resources { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; } = false;
    }

    /// <summary>
    /// MCP resource content
    /// </summary>
    public class McpResourceContent
    {
        public string Uri { get; set; } = string.Empty;
        public string MimeType { get; set; } = "application/json";
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// List of MCP tools
    /// </summary>
    public class McpToolList
    {
        public List<McpTool> Tools { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; } = false;
    }

    /// <summary>
    /// MCP tool execution result
    /// </summary>
    public class McpToolResult
    {
        public bool IsSuccess { get; set; } = true;
        public object? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public long ExecutionTimeMs { get; set; }
    }
}
