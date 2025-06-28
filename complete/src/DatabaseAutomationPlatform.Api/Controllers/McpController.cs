using DatabaseAutomationPlatform.Api.Authorization;
using DatabaseAutomationPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DatabaseAutomationPlatform.Api.Controllers;

/// <summary>
/// Main MCP protocol endpoint controller
/// </summary>
[ApiController]
[Route("api/v1/mcp")]
[Authorize]
[EnableRateLimiting("fixed")]
public class McpController : ControllerBase
{
    private readonly IMcpService _mcpService;
    private readonly IRequestValidationService _validationService;
    private readonly ILogger<McpController> _logger;

    public McpController(
        IMcpService mcpService,
        IRequestValidationService validationService,
        ILogger<McpController> logger)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Main MCP protocol endpoint
    /// </summary>
    /// <param name="request">MCP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MCP response</returns>
    [HttpPost]
    [ProducesResponseType(typeof(McpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessRequest(
        [FromBody] McpRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
        
        _logger.LogInformation("MCP request received. Method: {Method}, CorrelationId: {CorrelationId}", 
            request?.Method, correlationId);

        // Validate request
        var validationResult = await _validationService.ValidateRequestAsync(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Invalid MCP request. Errors: {@Errors}", validationResult.Errors);
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The request failed validation",
                Instance = HttpContext.Request.Path,
                CorrelationId = correlationId,
                Errors = validationResult.Errors.ToDictionary(
                    e => e.Field,
                    e => new[] { e.Message })
            });
        }

        // Process request
        var response = await _mcpService.ProcessRequestAsync(request!, cancellationToken);
        
        _logger.LogInformation("MCP request processed successfully. CorrelationId: {CorrelationId}", correlationId);
        
        return Ok(response);
    }

    /// <summary>
    /// Get server capabilities
    /// </summary>
    /// <returns>Server capabilities</returns>
    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(McpCapabilities), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapabilities()
    {
        var capabilities = await _mcpService.GetCapabilitiesAsync();
        return Ok(capabilities);
    }

    /// <summary>
    /// List available tools
    /// </summary>
    /// <returns>List of tools</returns>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(IEnumerable<McpTool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTools()
    {
        var tools = await _mcpService.ListToolsAsync();
        return Ok(tools);
    }

    /// <summary>
    /// Execute a specific tool
    /// </summary>
    /// <param name="toolName">Tool name</param>
    /// <param name="parameters">Tool parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    [HttpPost("tools/{toolName}")]
    [ProducesResponseType(typeof(McpToolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteTool(
        string toolName,
        [FromBody] Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
        
        _logger.LogInformation("Tool execution requested. Tool: {ToolName}, CorrelationId: {CorrelationId}", 
            toolName, correlationId);

        // Validate tool parameters
        var validationResult = await _validationService.ValidateToolParametersAsync(toolName, parameters);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Invalid tool parameters. Tool: {ToolName}, Errors: {@Errors}", 
                toolName, validationResult.Errors);
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Parameters",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The tool parameters failed validation",
                Instance = HttpContext.Request.Path,
                CorrelationId = correlationId,
                Errors = validationResult.Errors.ToDictionary(
                    e => e.Field,
                    e => new[] { e.Message })
            });
        }

        // Execute tool
        var result = await _mcpService.ExecuteToolAsync(toolName, parameters, cancellationToken);
        
        if (result.IsError)
        {
            _logger.LogError("Tool execution failed. Tool: {ToolName}, Error: {Error}", 
                toolName, result.ErrorMessage);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Tool Execution Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path,
                CorrelationId = correlationId
            });
        }
        
        _logger.LogInformation("Tool executed successfully. Tool: {ToolName}, CorrelationId: {CorrelationId}", 
            toolName, correlationId);
        
        return Ok(result);
    }
}