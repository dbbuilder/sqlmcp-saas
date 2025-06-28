using DatabaseAutomationPlatform.Api.Authorization;
using DatabaseAutomationPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseAutomationPlatform.Api.Controllers;

/// <summary>
/// Controller demonstrating permission-based authorization for tools
/// </summary>
[ApiController]
[Route("api/v1/secure")]
[Authorize]
public class SecureToolsController : ControllerBase
{
    private readonly IMcpService _mcpService;
    private readonly IRequestValidationService _validationService;
    private readonly ILogger<SecureToolsController> _logger;

    public SecureToolsController(
        IMcpService mcpService,
        IRequestValidationService validationService,
        ILogger<SecureToolsController> logger)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute a read-only query (requires query:execute permission)
    /// </summary>
    [HttpPost("query")]
    [RequirePermission(Permissions.QueryExecute)]
    [ProducesResponseType(typeof(McpToolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteQuery(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object>
        {
            ["database"] = request.Database,
            ["query"] = request.Query,
            ["timeout"] = request.TimeoutSeconds
        };

        var validationResult = await _validationService.ValidateToolParametersAsync("query", parameters);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Parameters",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The query parameters failed validation",
                Errors = validationResult.Errors.ToDictionary(e => e.Field, e => new[] { e.Message })
            });
        }

        // Additional SQL validation
        var sqlValidation = await _validationService.ValidateSqlQueryAsync(request.Query);
        if (!sqlValidation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid SQL Query",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The SQL query failed security validation",
                Errors = sqlValidation.Errors.ToDictionary(e => e.Field, e => new[] { e.Message })
            });
        }

        var result = await _mcpService.ExecuteToolAsync("query", parameters, cancellationToken);
        
        if (result.IsError)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Query Execution Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.ErrorMessage
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Execute a command (requires command:execute permission and Developer role)
    /// </summary>
    [HttpPost("command")]
    [RequirePermission(Permissions.CommandExecute)]
    [Authorize(Policy = "Developer")]
    [ProducesResponseType(typeof(McpToolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteCommand(
        [FromBody] CommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object>
        {
            ["database"] = request.Database,
            ["command"] = request.Command,
            ["transaction"] = request.UseTransaction
        };

        var validationResult = await _validationService.ValidateToolParametersAsync("execute", parameters);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Parameters",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The command parameters failed validation",
                Errors = validationResult.Errors.ToDictionary(e => e.Field, e => new[] { e.Message })
            });
        }

        _logger.LogWarning("Command execution requested by user {User} for database {Database}",
            User.Identity?.Name, request.Database);

        var result = await _mcpService.ExecuteToolAsync("execute", parameters, cancellationToken);
        
        if (result.IsError)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Command Execution Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.ErrorMessage
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get schema information (requires schema:read permission)
    /// </summary>
    [HttpPost("schema")]
    [RequirePermission(Permissions.SchemaRead)]
    [ProducesResponseType(typeof(McpToolResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSchema(
        [FromBody] SchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object>
        {
            ["database"] = request.Database,
            ["objectType"] = request.ObjectType,
            ["objectName"] = request.ObjectName ?? string.Empty
        };

        var validationResult = await _validationService.ValidateToolParametersAsync("schema", parameters);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Parameters",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The schema parameters failed validation",
                Errors = validationResult.Errors.ToDictionary(e => e.Field, e => new[] { e.Message })
            });
        }

        var result = await _mcpService.ExecuteToolAsync("schema", parameters, cancellationToken);
        
        if (result.IsError)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Schema Retrieval Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.ErrorMessage
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Perform maintenance operations (requires maintenance:run permission and DBA role)
    /// </summary>
    [HttpPost("maintenance")]
    [RequirePermission(Permissions.MaintenanceRun)]
    [Authorize(Policy = "DBA")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RunMaintenance(
        [FromBody] MaintenanceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Maintenance operation {Operation} requested by DBA {User} for database {Database}",
            request.OperationType, User.Identity?.Name, request.Database);

        // TODO: Implement maintenance operations
        return Ok(new
        {
            message = "Maintenance operation scheduled",
            operation = request.OperationType,
            database = request.Database,
            scheduledBy = User.Identity?.Name,
            scheduledAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Admin-only endpoint for security audit (requires admin:full permission and Admin role)
    /// </summary>
    [HttpGet("audit")]
    [RequirePermission(Permissions.AdminFull)]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SecurityAudit()
    {
        _logger.LogWarning("Security audit accessed by admin {User}", User.Identity?.Name);

        // TODO: Implement security audit
        return Ok(new
        {
            message = "Security audit report",
            generatedAt = DateTimeOffset.UtcNow,
            generatedBy = User.Identity?.Name,
            totalUsers = 42,
            activeApiKeys = 15,
            recentFailedAttempts = 3
        });
    }
}

// Request models
public class QueryRequest
{
    public string Database { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public class CommandRequest
{
    public string Database { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool UseTransaction { get; set; } = true;
}

public class SchemaRequest
{
    public string Database { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string? ObjectName { get; set; }
}

public class MaintenanceRequest
{
    public string Database { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public Dictionary<string, object>? Options { get; set; }
}