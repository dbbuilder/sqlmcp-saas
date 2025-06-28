using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlServerMcpFunctions.Domain.Entities;
using SqlServerMcpFunctions.Domain.Interfaces;
using SqlServerMcpFunctions.Domain.ValueObjects;
using SqlServerMcpFunctions.Application.Interfaces;

namespace SqlServerMcpFunctions.Application.Services
{
    /// <summary>
    /// Core MCP server service implementation
    /// </summary>
    public class McpServerService : IMcpServer
    {
        private readonly IStoredProcedureExecutor _storedProcedureExecutor;
        private readonly IToolRegistry _toolRegistry;
        private readonly IResourceRegistry _resourceRegistry;
        private readonly ILogger<McpServerService> _logger;
        private readonly Dictionary<string, object> _serverMetadata;
        private bool _isInitialized = false;

        public McpServerService(
            IStoredProcedureExecutor storedProcedureExecutor,
            IToolRegistry toolRegistry,
            IResourceRegistry resourceRegistry,
            ILogger<McpServerService> logger)
        {
            _storedProcedureExecutor = storedProcedureExecutor ?? throw new ArgumentNullException(nameof(storedProcedureExecutor));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _resourceRegistry = resourceRegistry ?? throw new ArgumentNullException(nameof(resourceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _serverMetadata = new Dictionary<string, object>
            {
                ["name"] = "SQL Server MCP Functions",
                ["version"] = "1.0.0",
                ["description"] = "Secure SQL Server integration via Model Context Protocol",
                ["author"] = "Your Organization",
                ["capabilities"] = new[] { "resources", "tools", "logging" }
            };
        }

        /// <summary>
        /// Initialize the MCP server and load available tools and resources
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing MCP server...");

                // Initialize tool registry
                await _toolRegistry.LoadToolsAsync(cancellationToken);
                
                // Initialize resource registry
                await _resourceRegistry.LoadResourcesAsync(cancellationToken);

                _isInitialized = true;
                _logger.LogInformation("MCP server initialized successfully. Tools: {ToolCount}, Resources: {ResourceCount}",
                    await _toolRegistry.GetToolCountAsync(),
                    await _resourceRegistry.GetResourceCountAsync());
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MCP server");
                throw;
            }
        }

        /// <summary>
        /// Get server capabilities for MCP protocol negotiation
        /// </summary>
        public Task<McpServerCapabilities> GetCapabilitiesAsync()
        {
            var capabilities = new McpServerCapabilities
            {
                SupportsResources = true,
                SupportsTools = true,
                SupportsPrompts = false, // Not implemented in this version
                SupportsLogging = true,
                ServerVersion = "1.0.0",
                ProtocolVersion = "2024-11-05"
            };

            _logger.LogDebug("Returning server capabilities: {Capabilities}", capabilities);
            return Task.FromResult(capabilities);
        }

        /// <summary>
        /// List all available resources with pagination support
        /// </summary>
        public async Task<McpResourceList> ListResourcesAsync(string? cursor = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Listing resources with cursor: {Cursor}", cursor);
                
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("MCP server is not initialized. Call InitializeAsync first.");
                }

                var resources = await _resourceRegistry.ListResourcesAsync(cursor, cancellationToken);
                
                _logger.LogDebug("Found {ResourceCount} resources", resources.Resources.Count);
                return resources;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error listing resources");
                throw;
            }
        }

        /// <summary>
        /// Get specific resource content by URI
        /// </summary>
        public async Task<McpResourceContent> GetResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting resource: {Uri}", uri);
                
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("MCP server is not initialized. Call InitializeAsync first.");
                }

                if (string.IsNullOrWhiteSpace(uri))
                {
                    throw new ArgumentException("Resource URI cannot be null or empty", nameof(uri));
                }

                var resource = await _resourceRegistry.GetResourceAsync(uri, cancellationToken);
                
                if (resource == null)
                {
                    throw new KeyNotFoundException($"Resource not found: {uri}");
                }

                _logger.LogDebug("Successfully retrieved resource: {Uri}", uri);
                return resource;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting resource: {Uri}", uri);
                throw;
            }
        }

        /// <summary>
        /// List all available tools with pagination support
        /// </summary>
        public async Task<McpToolList> ListToolsAsync(string? cursor = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Listing tools with cursor: {Cursor}", cursor);
                
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("MCP server is not initialized. Call InitializeAsync first.");
                }

                var tools = await _toolRegistry.ListToolsAsync(cursor, cancellationToken);
                
                _logger.LogDebug("Found {ToolCount} tools", tools.Tools.Count);
                return tools;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error listing tools");
                throw;
            }
        }
        /// <summary>
        /// Execute a tool with provided parameters
        /// </summary>
        public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Executing tool: {ToolName} with correlation ID: {CorrelationId}", toolName, correlationId);
                
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("MCP server is not initialized. Call InitializeAsync first.");
                }

                if (string.IsNullOrWhiteSpace(toolName))
                {
                    throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
                }

                // Get tool definition
                var tool = await _toolRegistry.GetToolAsync(toolName, cancellationToken);
                if (tool == null)
                {
                    throw new KeyNotFoundException($"Tool not found: {toolName}");
                }

                // Validate parameters against tool schema
                await ValidateToolParametersAsync(tool, parameters, cancellationToken);

                // Execute the stored procedure
                var executionResult = await _storedProcedureExecutor.ExecuteAsync(
                    tool.DatabaseIdentifier,
                    tool.StoredProcedureName,
                    parameters ?? new Dictionary<string, object?>(),
                    cancellationToken);

                var executionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                var result = new McpToolResult
                {
                    IsSuccess = executionResult.IsSuccess,
                    Content = ConvertResultToMcpFormat(executionResult),
                    ErrorMessage = executionResult.ErrorMessage,
                    ExecutionTimeMs = (long)executionTimeMs,
                    Metadata = new Dictionary<string, object>
                    {
                        ["correlationId"] = correlationId,
                        ["toolName"] = toolName,
                        ["rowsAffected"] = executionResult.RowsAffected,
                        ["returnValue"] = executionResult.ReturnValue ?? 0,
                        ["outputParameters"] = executionResult.OutputParameters,
                        ["resultSetCount"] = executionResult.ResultSets.Count
                    }
                };

                _logger.LogInformation("Tool execution completed: {ToolName}, Success: {Success}, Duration: {Duration}ms, CorrelationId: {CorrelationId}",
                    toolName, result.IsSuccess, result.ExecutionTimeMs, correlationId);

                return result;
            }
            catch (System.Exception ex)
            {
                var executionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Tool execution failed: {ToolName}, Duration: {Duration}ms, CorrelationId: {CorrelationId}",
                    toolName, executionTimeMs, correlationId);

                return new McpToolResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = (long)executionTimeMs,
                    Metadata = new Dictionary<string, object>
                    {
                        ["correlationId"] = correlationId,
                        ["toolName"] = toolName,
                        ["errorType"] = ex.GetType().Name
                    }
                };
            }
        }

        /// <summary>
        /// Handle incoming MCP request messages
        /// </summary>
        public async Task<McpMessage> HandleRequestAsync(McpMessage request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Handling MCP request: {Method}, ID: {Id}", request.Method, request.Id);

                return request.Method switch
                {
                    "initialize" => await HandleInitializeRequestAsync(request, cancellationToken),
                    "resources/list" => await HandleListResourcesRequestAsync(request, cancellationToken),
                    "resources/read" => await HandleGetResourceRequestAsync(request, cancellationToken),
                    "tools/list" => await HandleListToolsRequestAsync(request, cancellationToken),
                    "tools/call" => await HandleExecuteToolRequestAsync(request, cancellationToken),
                    "capabilities" => await HandleCapabilitiesRequestAsync(request, cancellationToken),
                    _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
                };
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error handling MCP request: {Method}, ID: {Id}", request.Method, request.Id);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        /// <summary>
        /// Shutdown the MCP server gracefully
        /// </summary>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Shutting down MCP server...");
                
                // Perform any cleanup operations here
                await _toolRegistry.ShutdownAsync(cancellationToken);
                await _resourceRegistry.ShutdownAsync(cancellationToken);
                
                _isInitialized = false;
                
                _logger.LogInformation("MCP server shutdown completed");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error during MCP server shutdown");
                throw;
            }
        }
