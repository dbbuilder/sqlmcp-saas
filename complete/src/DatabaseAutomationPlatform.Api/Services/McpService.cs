using DatabaseAutomationPlatform.Api.Extensions;
using DatabaseAutomationPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Implementation of MCP service
/// </summary>
public class McpService : IMcpService
{
    private readonly McpConfiguration _configuration;
    private readonly ILogger<McpService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public McpService(
        IOptions<McpConfiguration> configuration, 
        ILogger<McpService> logger,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<McpResponse> ProcessRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        _logger.LogInformation("Processing MCP request: {Method}", request.Method);

        try
        {
            object? result = request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request.Params, cancellationToken),
                "tools/list" => await ListToolsAsync(),
                "tools/call" => await HandleToolCallAsync(request.Params, cancellationToken),
                "resources/list" => await HandleResourcesListAsync(cancellationToken),
                "prompts/list" => await HandlePromptsListAsync(cancellationToken),
                _ => throw new NotSupportedException($"Method '{request.Method}' is not supported")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP request");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = "Internal error",
                    Data = new { details = ex.Message }
                }
            };
        }
    }

    public Task<McpCapabilities> GetCapabilitiesAsync()
    {
        return Task.FromResult(new McpCapabilities
        {
            Tools = new[] { "query", "execute", "schema", "analyze" },
            Resources = new[] { "databases", "tables", "procedures", "views" },
            Prompts = new[] { "sql-generation", "optimization", "security-review" },
            Experimental = new Dictionary<string, object>
            {
                ["streaming"] = true,
                ["batch"] = true
            }
        });
    }

    public Task<IEnumerable<McpTool>> ListToolsAsync()
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "query",
                Description = "Execute a read-only SQL query",
                InputSchema = new Dictionary<string, McpToolParameter>
                {
                    ["database"] = new McpToolParameter { Type = "string", Description = "Target database name", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "SQL query to execute", Required = true },
                    ["timeout"] = new McpToolParameter { Type = "integer", Description = "Query timeout in seconds", Required = false, Default = 30 }
                }
            },
            new McpTool
            {
                Name = "execute",
                Description = "Execute a SQL command (INSERT, UPDATE, DELETE)",
                InputSchema = new Dictionary<string, McpToolParameter>
                {
                    ["database"] = new McpToolParameter { Type = "string", Description = "Target database name", Required = true },
                    ["command"] = new McpToolParameter { Type = "string", Description = "SQL command to execute", Required = true },
                    ["transaction"] = new McpToolParameter { Type = "boolean", Description = "Execute within a transaction", Required = false, Default = true }
                }
            },
            new McpTool
            {
                Name = "schema",
                Description = "Get schema information for a database object",
                InputSchema = new Dictionary<string, McpToolParameter>
                {
                    ["database"] = new McpToolParameter { Type = "string", Description = "Target database name", Required = true },
                    ["objectType"] = new McpToolParameter { Type = "string", Description = "Object type (table, view, procedure)", Required = true },
                    ["objectName"] = new McpToolParameter { Type = "string", Description = "Object name", Required = false }
                }
            },
            new McpTool
            {
                Name = "analyze",
                Description = "Analyze query performance or data patterns",
                InputSchema = new Dictionary<string, McpToolParameter>
                {
                    ["database"] = new McpToolParameter { Type = "string", Description = "Target database name", Required = true },
                    ["analysisType"] = new McpToolParameter { Type = "string", Description = "Type of analysis (performance, statistics, patterns)", Required = true },
                    ["target"] = new McpToolParameter { Type = "string", Description = "Target query or table", Required = true }
                }
            }
        };

        return Task.FromResult<IEnumerable<McpTool>>(tools);
    }

    public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentException("Tool name cannot be empty", nameof(toolName));
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        _logger.LogInformation("Executing tool: {ToolName}", toolName);

        try
        {
            return toolName switch
            {
                "query" => await ExecuteQueryToolAsync(parameters, cancellationToken),
                "execute" => await ExecuteCommandToolAsync(parameters, cancellationToken),
                "schema" => await ExecuteSchemaToolAsync(parameters, cancellationToken),
                "analyze" => await ExecuteAnalyzeToolAsync(parameters, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<object> HandleInitializeAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var capabilities = await GetCapabilitiesAsync();
        return new
        {
            protocolVersion = "1.0",
            capabilities,
            serverInfo = new
            {
                name = _configuration.ServerName,
                version = _configuration.Version
            }
        };
    }

    private async Task<object> HandleToolCallAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (parameters == null || !parameters.TryGetValue("name", out var toolName))
        {
            throw new ArgumentException("Tool name is required");
        }

        var toolParams = parameters.TryGetValue("arguments", out var args) && args is Dictionary<string, object> dict
            ? dict
            : new Dictionary<string, object>();

        var result = await ExecuteToolAsync(toolName.ToString()!, toolParams, cancellationToken);
        return result;
    }

    private Task<object> HandleResourcesListAsync(CancellationToken cancellationToken)
    {
        // TODO: Implement resource listing when repository pattern is ready
        return Task.FromResult<object>(new { resources = Array.Empty<object>() });
    }

    private Task<object> HandlePromptsListAsync(CancellationToken cancellationToken)
    {
        // TODO: Implement prompts listing when AI services are ready
        return Task.FromResult<object>(new { prompts = Array.Empty<object>() });
    }

    private async Task<McpToolResult> ExecuteQueryToolAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate parameters
        if (!parameters.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'database' is required"
            };
        }

        if (!parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'query' is required"
            };
        }

        var timeout = 30;
        if (parameters.TryGetValue("timeout", out var timeoutValue))
        {
            if (!int.TryParse(timeoutValue?.ToString(), out timeout) || timeout < 1 || timeout > 300)
            {
                timeout = 30;
            }
        }

        // Get developer service
        var developerService = _serviceProvider.GetService<IDeveloperService>();
        if (developerService == null)
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Developer service not available"
            };
        }

        // Execute query
        var result = await developerService.ExecuteQueryAsync(
            database.ToString()!,
            query.ToString()!,
            timeout,
            cancellationToken);

        return new McpToolResult
        {
            Content = new
            {
                columns = result.Columns,
                rows = result.Rows,
                rowCount = result.RowCount,
                executionTimeMs = result.ExecutionTimeMs
            }
        };
    }

    private async Task<McpToolResult> ExecuteCommandToolAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate parameters
        if (!parameters.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'database' is required"
            };
        }

        if (!parameters.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'command' is required"
            };
        }

        var useTransaction = true;
        if (parameters.TryGetValue("transaction", out var transactionValue))
        {
            if (bool.TryParse(transactionValue?.ToString(), out var trans))
            {
                useTransaction = trans;
            }
        }

        // Get developer service
        var developerService = _serviceProvider.GetService<IDeveloperService>();
        if (developerService == null)
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Developer service not available"
            };
        }

        // Execute command
        var result = await developerService.ExecuteCommandAsync(
            database.ToString()!,
            command.ToString()!,
            useTransaction,
            cancellationToken);

        return new McpToolResult
        {
            Content = new
            {
                affectedRows = result.AffectedRows,
                success = result.Success,
                message = result.Message,
                executionTimeMs = result.ExecutionTimeMs
            }
        };
    }

    private async Task<McpToolResult> ExecuteSchemaToolAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate parameters
        if (!parameters.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'database' is required"
            };
        }

        if (!parameters.TryGetValue("objectType", out var objectType) || string.IsNullOrWhiteSpace(objectType?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'objectType' is required"
            };
        }

        var objectName = parameters.TryGetValue("objectName", out var name) ? name?.ToString() : null;

        // Get schema service
        var schemaService = _serviceProvider.GetService<ISchemaService>();
        if (schemaService == null)
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Schema service not available"
            };
        }

        // Parse object type
        if (!Enum.TryParse<ObjectType>(objectType.ToString(), true, out var objType))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = $"Invalid object type: {objectType}"
            };
        }

        // Get schema info
        var result = await schemaService.GetSchemaInfoAsync(
            database.ToString()!,
            objType,
            objectName,
            cancellationToken);

        return new McpToolResult
        {
            Content = result
        };
    }

    private async Task<McpToolResult> ExecuteAnalyzeToolAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        // Validate parameters
        if (!parameters.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'database' is required"
            };
        }

        if (!parameters.TryGetValue("analysisType", out var analysisType) || string.IsNullOrWhiteSpace(analysisType?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'analysisType' is required"
            };
        }

        if (!parameters.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target?.ToString()))
        {
            return new McpToolResult
            {
                IsError = true,
                ErrorMessage = "Parameter 'target' is required"
            };
        }

        var analysisTypeStr = analysisType.ToString()!.ToLower();

        switch (analysisTypeStr)
        {
            case "performance":
                var dbaService = _serviceProvider.GetService<IDbaService>();
                if (dbaService == null)
                {
                    return new McpToolResult
                    {
                        IsError = true,
                        ErrorMessage = "DBA service not available"
                    };
                }

                var perfResult = await dbaService.AnalyzePerformanceAsync(
                    database.ToString()!,
                    target.ToString()!,
                    cancellationToken);

                return new McpToolResult
                {
                    Content = perfResult
                };

            case "statistics":
            case "patterns":
                var analyticsService = _serviceProvider.GetService<IAnalyticsService>();
                if (analyticsService == null)
                {
                    return new McpToolResult
                    {
                        IsError = true,
                        ErrorMessage = "Analytics service not available"
                    };
                }

                if (analysisTypeStr == "statistics")
                {
                    var statsResult = await analyticsService.ProfileDataAsync(
                        database.ToString()!,
                        target.ToString()!,
                        new ProfileOptions(),
                        cancellationToken);

                    return new McpToolResult
                    {
                        Content = statsResult
                    };
                }
                else
                {
                    var insights = await analyticsService.GenerateInsightsAsync(
                        database.ToString()!,
                        $"SELECT * FROM {target}",
                        new InsightOptions(),
                        cancellationToken);

                    return new McpToolResult
                    {
                        Content = insights
                    };
                }

            default:
                return new McpToolResult
                {
                    IsError = true,
                    ErrorMessage = $"Unknown analysis type: {analysisType}"
                };
        }
    }
}