using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.HealthChecks;
using DatabaseAutomationPlatform.Infrastructure.Logging;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Domain.Interfaces;

namespace DatabaseAutomationPlatform.Infrastructure
{
    /// <summary>
    /// Extension methods for configuring infrastructure services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all infrastructure services to the dependency injection container
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Add configuration options
            services.AddDatabaseConfiguration(configuration);
            
            // Add security services
            services.AddSecurityServices(configuration);
            
            // Add data access services
            services.AddDataAccessServices(configuration);
            
            // Add health checks
            services.AddHealthCheckServices(configuration);
            
            return services;
        }

        /// <summary>
        /// Adds database configuration options
        /// </summary>
        private static IServiceCollection AddDatabaseConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<DatabaseOptions>(
                configuration.GetSection("Database"));
            
            return services;
        }

        /// <summary>
        /// Adds security-related services
        /// </summary>
        private static IServiceCollection AddSecurityServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register Azure Key Vault secret manager
            services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
            
            // Register secure connection string provider
            services.AddSingleton<ISecureConnectionStringProvider, SecureConnectionStringProvider>();
            
            // Register security logger
            services.AddSingleton<ISecurityLogger, SecurityLogger>();
            
            return services;
        }

        /// <summary>
        /// Adds data access services
        /// </summary>
        private static IServiceCollection AddDataAccessServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register database connection factory
            services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
            
            // Register stored procedure executor
            services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
            
            // Configure Polly policies
            services.AddSingleton<IAsyncPolicy>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SqlConnectionFactory>>();
                
                return Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        onRetry: (exception, timeSpan, retryCount, context) =>
                        {
                            logger.LogWarning(
                                exception,
                                "Database operation retry {RetryCount} after {TimeSpan}s",
                                retryCount,
                                timeSpan.TotalSeconds);
                        });
            });
            
            return services;
        }

        /// <summary>
        /// Adds health check services
        /// </summary>
        private static IServiceCollection AddHealthCheckServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var healthChecksBuilder = services.AddHealthChecks();
            
            // Add Azure Key Vault health check
            healthChecksBuilder.AddTypeActivatedCheck<AzureKeyVaultHealthCheck>(
                "azure-keyvault",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "security", "infrastructure" });
            
            // Add SQL Server health check
            var connectionStringName = configuration["Database:ConnectionStringName"] ?? "DefaultConnection";
            healthChecksBuilder.AddTypeActivatedCheck<SqlServerHealthCheck>(
                "sql-server",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database", "infrastructure" },
                args: new object[] { connectionStringName });
            
            return services;
        }

        /// <summary>
        /// Adds Serilog logging configuration
        /// </summary>
        public static IServiceCollection AddSerilogLogging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<LoggingOptions>(
                configuration.GetSection("Logging"));
            
            services.AddSingleton<SerilogConfiguration>();
            
            // Add correlation ID enricher
            services.AddSingleton<CorrelationIdEnricher>();
            
            return services;
        }

        /// <summary>
        /// Configures options with validation
        /// </summary>
        public static IServiceCollection ConfigureOptionsWithValidation<TOptions>(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName)
            where TOptions : class
        {
            services.Configure<TOptions>(configuration.GetSection(sectionName));
            
            services.AddSingleton<IValidateOptions<TOptions>>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<TOptions>>();
                return new DataAnnotationValidateOptions<TOptions>(sectionName, logger);
            });
            
            return services;
        }
    }

    /// <summary>
    /// Options validator using data annotations
    /// </summary>
    internal class DataAnnotationValidateOptions<TOptions> : IValidateOptions<TOptions>
        where TOptions : class
    {
        private readonly string _name;
        private readonly ILogger _logger;

        public DataAnnotationValidateOptions(string name, ILogger logger)
        {
            _name = name;
            _logger = logger;
        }

        public ValidateOptionsResult Validate(string name, TOptions options)
        {
            if (name != null && name != _name)
            {
                return ValidateOptionsResult.Skip;
            }

            var validationResults = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                options,
                validationContext,
                validationResults,
                validateAllProperties: true);

            if (isValid)
            {
                return ValidateOptionsResult.Success;
            }

            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            _logger.LogError("Configuration validation failed for {OptionsType}: {Errors}", 
                typeof(TOptions).Name, errors);

            return ValidateOptionsResult.Fail(errors);
        }
    }
}