using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlServerMcpFunctions.Infrastructure.DependencyInjection;
using Azure.Identity;

namespace SqlServerMcpFunctions
{
    /// <summary>
    /// Entry point for Azure Functions host with comprehensive configuration
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog for early logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting Azure Functions MCP Server host");

                var host = new HostBuilder()
                    .ConfigureFunctionsWebApplication()
                    .ConfigureAppConfiguration(ConfigureAppConfiguration)
                    .ConfigureServices(ConfigureServices)
                    .ConfigureLogging(ConfigureLogging)
                    .Build();

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Azure Functions host terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Configure application configuration sources including Key Vault
        /// </summary>
        private static void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder config)
        {
            var env = context.HostingEnvironment;

            // Add configuration files
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                  .AddEnvironmentVariables();

            // Add Azure Key Vault if configured
            var builtConfig = config.Build();
            var keyVaultUri = builtConfig["Azure:KeyVault:VaultUri"];
            
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                config.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential());
                
                Log.Information("Added Azure Key Vault configuration: {VaultUri}", keyVaultUri);
            }
        }

        /// <summary>
        /// Configure dependency injection services
        /// </summary>
        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            var configuration = context.Configuration;

            // Add Application Insights
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();

            // Add infrastructure services
            services.AddInfrastructureServices(configuration);

            // Configure options
            services.Configure<McpServerOptions>(configuration.GetSection("McpServer"));

            Log.Information("Configured dependency injection services for environment: {Environment}", 
                context.HostingEnvironment.EnvironmentName);
        }

        /// <summary>
        /// Configure logging with Serilog
        /// </summary>
        private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logging)
        {
            var configuration = context.Configuration;

            // Clear default logging providers
            logging.ClearProviders();

            // Configure Serilog
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "SqlServerMcpFunctions")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .CreateLogger();

            logging.AddSerilog(logger);
            Log.Logger = logger;

            Log.Information("Configured Serilog logging for Azure Functions");
        }
    }

    /// <summary>
    /// Configuration options for MCP server
    /// </summary>
    public class McpServerOptions
    {
        public int MaxConcurrentRequests { get; set; } = 100;
        public int QueryTimeoutSeconds { get; set; } = 30;
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public bool EnableDetailedLogging { get; set; } = false;
        public string ServerVersion { get; set; } = "1.0.0";
        public string ProtocolVersion { get; set; } = "2024-11-05";
    }
}
