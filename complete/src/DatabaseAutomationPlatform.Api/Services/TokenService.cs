using DatabaseAutomationPlatform.Api.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Implementation of JWT token service
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<TokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenService(IOptions<JwtSettings> jwtSettings, ILogger<TokenService> logger)
    {
        _jwtSettings = jwtSettings?.Value ?? throw new ArgumentNullException(nameof(jwtSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public Task<TokenResponse> GenerateAccessTokenAsync(string userId, string[] roles, Dictionary<string, string>? claims = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (roles == null || roles.Length == 0) throw new ArgumentException("At least one role must be specified", nameof(roles));

        var tokenClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add roles
        foreach (var role in roles)
        {
            tokenClaims.Add(new Claim("role", role));
        }

        // Add custom claims
        if (claims != null)
        {
            foreach (var claim in claims)
            {
                tokenClaims.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: tokenClaims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials
        );

        var tokenString = _tokenHandler.WriteToken(token);
        var refreshToken = GenerateRefreshTokenAsync().Result;

        _logger.LogInformation("Generated access token for user {UserId} with roles {Roles}", userId, string.Join(", ", roles));

        return Task.FromResult(new TokenResponse
        {
            AccessToken = tokenString,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
            ExpiresAt = expiresAt
        });
    }

    public Task<string> GenerateRefreshTokenAsync()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Task.FromResult(Convert.ToBase64String(randomNumber));
    }

    public Task<TokenResponse> RefreshAccessTokenAsync(string refreshToken)
    {
        // TODO: Implement refresh token validation with database lookup
        throw new NotImplementedException("Refresh token validation requires database implementation");
    }

    public Task RevokeRefreshTokenAsync(string refreshToken)
    {
        // TODO: Implement refresh token revocation with database update
        throw new NotImplementedException("Refresh token revocation requires database implementation");
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }
}