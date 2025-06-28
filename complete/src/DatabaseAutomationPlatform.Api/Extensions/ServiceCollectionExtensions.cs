using DatabaseAutomationPlatform.Api.Services;
using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Application.Services;
using Microsoft.Extensions.Options;

namespace DatabaseAutomationPlatform.Api.Extensions;

/// <summary>
/// Extension methods for registering API services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds API-specific services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Register API configuration
        services.Configure<McpConfiguration>(configuration.GetSection("Mcp"));
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<RateLimitSettings>(configuration.GetSection("RateLimit"));

        // Register API services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IMcpService, McpService>();
        services.AddScoped<IRequestValidationService, RequestValidationService>();

        // Register Application services
        services.AddScoped<IDeveloperService, DeveloperService>();
        services.AddScoped<IDbaService, DbaService>();
        services.AddScoped<ISchemaService, SchemaService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Register background services
        services.AddHostedService<HealthCheckBackgroundService>();
        services.AddHostedService<MetricsCollectionService>();

        // Add memory cache for performance
        services.AddMemoryCache();

        // Add distributed cache (Redis in production, in-memory for development)
        if (configuration.GetValue<bool>("UseRedisCache"))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "DatabaseAutomation";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}

/// <summary>
/// MCP configuration settings
/// </summary>
public class McpConfiguration
{
    public string ServerName { get; set; } = "DatabaseAutomationPlatform";
    public string Version { get; set; } = "1.0.0";
    public int MaxRequestSize { get; set; } = 1048576; // 1MB
    public int RequestTimeout { get; set; } = 30000; // 30 seconds
    public string[] SupportedCapabilities { get; set; } = Array.Empty<string>();
}

/// <summary>
/// JWT authentication settings
/// </summary>
public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// Rate limiting settings
/// </summary>
public class RateLimitSettings
{
    public bool Enabled { get; set; } = true;
    public int RequestsPerMinute { get; set; } = 60;
    public int RequestsPerHour { get; set; } = 1000;
    public string[] WhitelistedIPs { get; set; } = Array.Empty<string>();
}