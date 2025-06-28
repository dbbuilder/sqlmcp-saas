using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Logging;
using DatabaseAutomationPlatform.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DatabaseAutomationPlatform.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering infrastructure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all infrastructure services to the DI container
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Add configuration services
        services.AddConfigurationServices(configuration);

        // Add security services  
        services.AddSecurityServices(configuration);

        // Add logging services
        services.AddLoggingServices(configuration);

        // Add data access services
        services.AddDataAccessServices(configuration);

        // Add repository services
        services.AddRepositoryServices();

        return services;
    }

    /// <summary>
    /// Add configuration services
    /// </summary>
    private static IServiceCollection AddConfigurationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Azure Key Vault configuration is already added in Infrastructure
        services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
        
        return services;
    }

    /// <summary>
    /// Add security services
    /// </summary>
    private static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add any security services
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        services.AddScoped<IHashingService, HashingService>();
        
        return services;
    }

    /// <summary>
    /// Add logging services
    /// </summary>
    private static IServiceCollection AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Serilog configuration is already set up in Program.cs
        // Add any additional logging services here
        
        return services;
    }

    /// <summary>
    /// Add data access services
    /// </summary>
    private static IServiceCollection AddDataAccessServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register database connection factory
        services.AddSingleton<IDatabaseConnectionFactory, SecureDatabaseConnectionFactory>();
        
        // Register stored procedure executor
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        
        // Add health checks for database
        services.AddHealthChecks()
            .AddSqlServer(
                configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured"),
                name: "database",
                tags: new[] { "db", "sql" });

        return services;
    }
}

/// <summary>
/// Placeholder encryption service interface
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

/// <summary>
/// Placeholder hashing service interface
/// </summary>
public interface IHashingService
{
    string Hash(string input);
    bool Verify(string input, string hash);
}

/// <summary>
/// Placeholder encryption service implementation
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText)
    {
        // TODO: Implement AES encryption
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));
    }

    public string Decrypt(string cipherText)
    {
        // TODO: Implement AES decryption
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
    }
}

/// <summary>
/// Placeholder hashing service implementation
/// </summary>
public class HashingService : IHashingService
{
    public string Hash(string input)
    {
        // TODO: Implement proper hashing (e.g., BCrypt)
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    public bool Verify(string input, string hash)
    {
        return Hash(input) == hash;
    }
}