using System.Threading;
using System.Threading.Tasks;
using SqlServerMcpFunctions.Domain.Entities;
using SqlServerMcpFunctions.Domain.Interfaces;

namespace SqlServerMcpFunctions.Application.Interfaces
{
    /// <summary>
    /// Interface for managing MCP tools
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>
        /// Load all available tools from configuration and database
        /// </summary>
        Task LoadToolsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// List all tools with pagination
        /// </summary>
        Task<McpToolList> ListToolsAsync(string? cursor = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific tool by name
        /// </summary>
        Task<McpTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get total number of available tools
        /// </summary>
        Task<int> GetToolCountAsync();

        /// <summary>
        /// Shutdown the tool registry
        /// </summary>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
    }
}
