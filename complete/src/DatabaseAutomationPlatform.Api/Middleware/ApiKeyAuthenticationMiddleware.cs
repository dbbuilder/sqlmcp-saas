using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace DatabaseAutomationPlatform.Api.Middleware;

/// <summary>
/// Authentication handler for API key authentication
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IApiKeyValidationService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidationService apiKeyService) : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key in header
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        // Validate API key
        var validationResult = await _apiKeyService.ValidateApiKeyAsync(apiKey);
        if (!validationResult.IsValid)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, validationResult.ClientId),
            new Claim(ClaimTypes.Name, validationResult.ClientName),
            new Claim("client_id", validationResult.ClientId),
            new Claim("api_key_id", validationResult.ApiKeyId)
        };

        // Add roles
        foreach (var role in validationResult.Roles)
        {
            claims.Add(new Claim("role", role));
        }

        // Add permissions
        foreach (var permission in validationResult.Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Options for API key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
}

/// <summary>
/// Service for validating API keys
/// </summary>
public interface IApiKeyValidationService
{
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey);
    Task<string> GenerateApiKeyAsync(string clientId, string[] roles, string[] permissions);
    Task RevokeApiKeyAsync(string apiKey);
}

/// <summary>
/// Result of API key validation
/// </summary>
public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public string ApiKeyId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Implementation of API key validation service
/// </summary>
public class ApiKeyValidationService : IApiKeyValidationService
{
    private readonly ILogger<ApiKeyValidationService> _logger;
    
    // TODO: Replace with database storage
    private static readonly Dictionary<string, ApiKeyValidationResult> _apiKeys = new()
    {
        ["demo-api-key-12345"] = new ApiKeyValidationResult
        {
            IsValid = true,
            ApiKeyId = "key1",
            ClientId = "demo-client",
            ClientName = "Demo Client",
            Roles = new[] { "Developer", "Viewer" },
            Permissions = new[] { "query:execute", "schema:read" }
        }
    };

    public ApiKeyValidationService(ILogger<ApiKeyValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new ApiKeyValidationResult { IsValid = false });
        }

        // TODO: Implement database lookup with caching
        if (_apiKeys.TryGetValue(apiKey, out var result))
        {
            // Check expiration
            if (result.ExpiresAt.HasValue && result.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("API key {ApiKeyId} has expired", result.ApiKeyId);
                return Task.FromResult(new ApiKeyValidationResult { IsValid = false });
            }

            _logger.LogInformation("API key {ApiKeyId} validated for client {ClientId}", 
                result.ApiKeyId, result.ClientId);
            return Task.FromResult(result);
        }

        _logger.LogWarning("Invalid API key attempted");
        return Task.FromResult(new ApiKeyValidationResult { IsValid = false });
    }

    public Task<string> GenerateApiKeyAsync(string clientId, string[] roles, string[] permissions)
    {
        // TODO: Implement secure API key generation with database storage
        var apiKey = $"{clientId}-{Guid.NewGuid():N}";
        return Task.FromResult(apiKey);
    }

    public Task RevokeApiKeyAsync(string apiKey)
    {
        // TODO: Implement API key revocation with database update
        if (_apiKeys.ContainsKey(apiKey))
        {
            _apiKeys.Remove(apiKey);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for API key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme, configureOptions);
    }
}