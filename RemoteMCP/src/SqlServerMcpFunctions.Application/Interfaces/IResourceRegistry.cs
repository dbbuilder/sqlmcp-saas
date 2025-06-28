using System.Threading;
using System.Threading.Tasks;
using SqlServerMcpFunctions.Domain.Interfaces;

namespace SqlServerMcpFunctions.Application.Interfaces
{
    /// <summary>
    /// Interface for managing MCP resources
    /// </summary>
    public interface IResourceRegistry
    {
        /// <summary>
        /// Load all available resources from configuration and database
        /// </summary>
        Task LoadResourcesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// List all resources with pagination
        /// </summary>
        Task<McpResourceList> ListResourcesAsync(string? cursor = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific resource by URI
        /// </summary>
        Task<McpResourceContent?> GetResourceAsync(string uri, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get total number of available resources
        /// </summary>
        Task<int> GetResourceCountAsync();

        /// <summary>
        /// Shutdown the resource registry
        /// </summary>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
    }
}
