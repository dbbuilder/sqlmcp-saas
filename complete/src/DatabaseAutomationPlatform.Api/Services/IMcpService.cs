namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Service for handling MCP protocol operations
/// </summary>
public interface IMcpService
{
    /// <summary>
    /// Processes an MCP request and returns the response
    /// </summary>
    Task<McpResponse> ProcessRequestAsync(McpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server capabilities
    /// </summary>
    Task<McpCapabilities> GetCapabilitiesAsync();

    /// <summary>
    /// Lists available tools
    /// </summary>
    Task<IEnumerable<McpTool>> ListToolsAsync();

    /// <summary>
    /// Executes a specific tool
    /// </summary>
    Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP request model
/// </summary>
public class McpRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
    public object? Id { get; set; }
}

/// <summary>
/// MCP response model
/// </summary>
public class McpResponse
{
    public string JsonRpc { get; set; } = "2.0";
    public object? Result { get; set; }
    public McpError? Error { get; set; }
    public object? Id { get; set; }
}

/// <summary>
/// MCP error model
/// </summary>
public class McpError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

/// <summary>
/// MCP capabilities model
/// </summary>
public class McpCapabilities
{
    public string[] Tools { get; set; } = Array.Empty<string>();
    public string[] Resources { get; set; } = Array.Empty<string>();
    public string[] Prompts { get; set; } = Array.Empty<string>();
    public Dictionary<string, object>? Experimental { get; set; }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, McpToolParameter> InputSchema { get; set; } = new();
}

/// <summary>
/// MCP tool parameter definition
/// </summary>
public class McpToolParameter
{
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
    public object? Default { get; set; }
}

/// <summary>
/// MCP tool execution result
/// </summary>
public class McpToolResult
{
    public object? Content { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}