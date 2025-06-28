using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SqlServerMcpFunctions.Domain.Interfaces;

namespace SqlServerMcpFunctions.Functions
{
    /// <summary>
    /// Azure Functions implementation of MCP server endpoints
    /// </summary>
    public class McpServerFunctions
    {
        private readonly IMcpServer _mcpServer;
        private readonly ILogger<McpServerFunctions> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpServerFunctions(
            IMcpServer mcpServer,
            ILogger<McpServerFunctions> logger)
        {
            _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Initialize MCP server and return capabilities
        /// </summary>
        [Function("Initialize")]
        public async Task<HttpResponseData> InitializeAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mcp/initialize")] HttpRequestData req,
            FunctionContext context)
        {
            var correlationId = Guid.NewGuid().ToString();
            
            try
            {
                _logger.LogInformation("MCP Initialize request received - CorrelationId: {CorrelationId}", correlationId);

                // Initialize the MCP server
                await _mcpServer.InitializeAsync(context.CancellationToken);

                // Get server capabilities
                var capabilities = await _mcpServer.GetCapabilitiesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("X-Correlation-ID", correlationId);

                var result = new
                {
                    jsonrpc = "2.0",
                    id = ExtractRequestId(req),
                    result = new
                    {
                        protocolVersion = capabilities.ProtocolVersion,
                        capabilities = new
                        {
                            resources = new { listChanged = capabilities.SupportsResources },
                            tools = new { listChanged = capabilities.SupportsTools },
                            prompts = new { listChanged = capabilities.SupportsPrompts },
                            logging = new { }
                        },
                        serverInfo = new
                        {
                            name = "SQL Server MCP Functions",
                            version = capabilities.ServerVersion
                        }
                    }
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(result, _jsonOptions));

                _logger.LogInformation("MCP Initialize completed successfully - CorrelationId: {CorrelationId}", correlationId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP Initialize failed - CorrelationId: {CorrelationId}", correlationId);
                return await CreateErrorResponseAsync(req, correlationId, -32603, "Internal error during initialization");
            }
        }

        /// <summary>
        /// Health check endpoint for monitoring
        /// </summary>
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheckAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
            FunctionContext context)
        {
            try
            {
                // Perform basic health checks
                var capabilities = await _mcpServer.GetCapabilitiesAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");

                var result = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    version = capabilities.ServerVersion,
                    functionApp = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "local"
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(result, _jsonOptions));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                
                var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                response.Headers.Add("Content-Type", "application/json");

                var result = new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(result, _jsonOptions));
                return response;
            }
        }
