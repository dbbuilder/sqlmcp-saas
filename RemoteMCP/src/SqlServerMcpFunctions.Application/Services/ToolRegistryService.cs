using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlServerMcpFunctions.Domain.Entities;
using SqlServerMcpFunctions.Domain.Interfaces;
using SqlServerMcpFunctions.Application.Interfaces;

namespace SqlServerMcpFunctions.Application.Services
{
    /// <summary>
    /// Service for managing and discovering available MCP tools
    /// </summary>
    public class ToolRegistryService : IToolRegistry
    {
        private readonly IStoredProcedureExecutor _storedProcedureExecutor;
        private readonly ILogger<ToolRegistryService> _logger;
        private readonly List<McpTool> _tools = new();
        private readonly object _lockObject = new();
        private bool _isLoaded = false;

        public ToolRegistryService(
            IStoredProcedureExecutor storedProcedureExecutor,
            ILogger<ToolRegistryService> logger)
        {
            _storedProcedureExecutor = storedProcedureExecutor ?? throw new ArgumentNullException(nameof(storedProcedureExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Load all available tools from database metadata
        /// </summary>
        public async Task LoadToolsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Loading available tools...");

                lock (_lockObject)
                {
                    _tools.Clear();
                }

                // Discover tools from stored procedures
                // This is a simplified implementation - in practice, you might have a configuration table
                // or use stored procedure metadata to automatically discover available tools
                await LoadPredefinedToolsAsync(cancellationToken);
                await DiscoverStoredProcedureToolsAsync(cancellationToken);

                lock (_lockObject)
                {
                    _isLoaded = true;
                }

                _logger.LogInformation("Loaded {ToolCount} tools successfully", _tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tools");
                throw;
            }
        }

        /// <summary>
        /// List all tools with pagination support
        /// </summary>
        public Task<McpToolList> ListToolsAsync(string? cursor = null, CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                if (!_isLoaded)
                {
                    throw new InvalidOperationException("Tools not loaded. Call LoadToolsAsync first.");
                }

                // Simple pagination implementation
                const int pageSize = 50;
                int startIndex = 0;

                if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out int cursorIndex))
                {
                    startIndex = cursorIndex;
                }

                var pagedTools = _tools.Skip(startIndex).Take(pageSize).ToList();
                var hasMore = _tools.Count > startIndex + pageSize;
                var nextCursor = hasMore ? (startIndex + pageSize).ToString() : null;

                return Task.FromResult(new McpToolList
                {
                    Tools = pagedTools,
                    NextCursor = nextCursor,
                    HasMore = hasMore
                });
            }
        }

        /// <summary>
        /// Get a specific tool by name
        /// </summary>
        public Task<McpTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
            }

            lock (_lockObject)
            {
                if (!_isLoaded)
                {
                    throw new InvalidOperationException("Tools not loaded. Call LoadToolsAsync first.");
                }

                var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(tool);
            }
        }

        /// <summary>
        /// Get total number of available tools
        /// </summary>
        public Task<int> GetToolCountAsync()
        {
            lock (_lockObject)
            {
                return Task.FromResult(_tools.Count);
            }
        }

        /// <summary>
        /// Shutdown the tool registry
        /// </summary>
        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                _tools.Clear();
                _isLoaded = false;
            }

            _logger.LogInformation("Tool registry shutdown completed");
            return Task.CompletedTask;
        }

        #region Private Methods

        /// <summary>
        /// Load predefined tools from configuration
        /// </summary>
        private async Task LoadPredefinedToolsAsync(CancellationToken cancellationToken)
        {
            // Example predefined tools - in practice, these could come from configuration
            var predefinedTools = new[]
            {
                new McpTool
                {
                    Name = "get_user_data",
                    Description = "Retrieve user information by ID",
                    StoredProcedureName = "GetUserById",
                    DatabaseIdentifier = "default",
                    InputSchema = JsonDocument.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""userId"": {
                                ""type"": ""integer"",
                                ""description"": ""The user ID to retrieve""
                            }
                        },
                        ""required"": [""userId""]
                    }"),
                    IsReadOnly = true,
                    RequiresAuthentication = true,
                    TimeoutSeconds = 30
                }
            };

            lock (_lockObject)
            {
                _tools.AddRange(predefinedTools);
            }

            _logger.LogDebug("Loaded {Count} predefined tools", predefinedTools.Length);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Discover tools by examining stored procedures in the database
        /// </summary>
        private async Task DiscoverStoredProcedureToolsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // This would query system tables to discover stored procedures
                // For now, we'll skip automatic discovery
                _logger.LogDebug("Stored procedure discovery completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover stored procedures automatically");
            }
        }

        #endregion
    }
}
