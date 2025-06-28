using DatabaseAutomationPlatform.Api.Models;
using DatabaseAutomationPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseAutomationPlatform.Api.Controllers;

/// <summary>
/// Authentication controller for managing tokens
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ITokenService tokenService, ILogger<AuthController> logger)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticate and get access token
    /// </summary>
    /// <param name="request">Login request</param>
    /// <returns>Token response</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // TODO: Implement actual authentication logic
        // This is a placeholder implementation
        if (request.Username != "demo" || request.Password != "demo123")
        {
            _logger.LogWarning("Authentication failed for user: {Username}", request.Username);
            return Unauthorized(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Title = "Authentication Failed",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Invalid username or password",
                Instance = HttpContext.Request.Path,
                CorrelationId = HttpContext.Items["CorrelationId"]?.ToString()
            });
        }

        // Generate token
        var roles = request.Username switch
        {
            "demo" => new[] { "Developer", "Viewer" },
            _ => new[] { "Viewer" }
        };

        var tokenResponse = await _tokenService.GenerateAccessTokenAsync(
            request.Username,
            roles,
            new Dictionary<string, string>
            {
                ["email"] = $"{request.Username}@example.com"
            });

        _logger.LogInformation("Token generated for user: {Username}", request.Username);

        return Ok(tokenResponse);
    }

    /// <summary>
    /// Refresh access token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New token response</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var tokenResponse = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken);
            _logger.LogInformation("Token refreshed successfully");
            return Ok(tokenResponse);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Invalid refresh token attempted");
            return Unauthorized(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Title = "Invalid Refresh Token",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The refresh token is invalid or expired",
                Instance = HttpContext.Request.Path,
                CorrelationId = HttpContext.Items["CorrelationId"]?.ToString()
            });
        }
    }

    /// <summary>
    /// Revoke refresh token
    /// </summary>
    /// <param name="request">Revoke token request</param>
    /// <returns>No content</returns>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        _logger.LogInformation("Refresh token revoked");
        
        return NoContent();
    }
}

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token request model
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Revoke token request model
/// </summary>
public class RevokeTokenRequest
{
    /// <summary>
    /// Refresh token to revoke
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}