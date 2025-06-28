namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Service for handling JWT token generation and validation
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user
    /// </summary>
    Task<TokenResponse> GenerateAccessTokenAsync(string userId, string[] roles, Dictionary<string, string>? claims = null);

    /// <summary>
    /// Generates a refresh token
    /// </summary>
    Task<string> GenerateRefreshTokenAsync();

    /// <summary>
    /// Validates a refresh token and returns a new access token
    /// </summary>
    Task<TokenResponse> RefreshAccessTokenAsync(string refreshToken);

    /// <summary>
    /// Revokes a refresh token
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Validates a JWT token and returns the claims principal
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}

/// <summary>
/// Token response model
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}