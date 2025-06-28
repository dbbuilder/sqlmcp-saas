using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

namespace DatabaseAutomationPlatform.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// Configuration setup for Serilog with Application Insights integration
    /// </summary>
    public static class SerilogConfiguration
    {
        /// <summary>
        /// Configures Serilog for the application
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="services">Service collection for dependency resolution</param>
        /// <returns>Logger configuration</returns>
        public static LoggerConfiguration ConfigureSerilog(
            IConfiguration configuration, 
            IServiceCollection services)
        {
            // Get logging options
            var loggingOptions = configuration.GetSection(LoggingOptions.SectionName)
                .Get<LoggingOptions>() ?? new LoggingOptions();
            
            // Validate options
            loggingOptions.Validate();

            // Build service provider to get dependencies
            var serviceProvider = services.BuildServiceProvider();
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            var hostEnvironment = serviceProvider.GetService<IHostEnvironment>();

            // Create logger configuration
            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("ApplicationName", "DatabaseAutomationPlatform")
                .Enrich.WithProperty("Version", typeof(SerilogConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0");

            // Add machine name enrichment
            if (loggingOptions.EnableMachineName)
            {
                loggerConfiguration.Enrich.WithMachineName();
            }

            // Add environment enrichment
            if (loggingOptions.EnableEnvironmentName && hostEnvironment != null)
            {
                loggerConfiguration.Enrich.WithProperty("Environment", hostEnvironment.EnvironmentName);
            }

            // Add correlation ID enrichment
            if (loggingOptions.EnableCorrelationId && httpContextAccessor != null)
            {
                loggerConfiguration.Enrich.With(new CorrelationIdEnricher(httpContextAccessor));
            }

            // Add custom properties
            foreach (var property in loggingOptions.CustomProperties)
            {
                loggerConfiguration.Enrich.WithProperty(property.Key, property.Value);
            }

            // Configure minimum levels
            loggerConfiguration.MinimumLevel.Is(ParseLogLevel(loggingOptions.MinimumLevel));
            loggerConfiguration.MinimumLevel.Override("Microsoft", ParseLogLevel(loggingOptions.MicrosoftMinimumLevel));
            loggerConfiguration.MinimumLevel.Override("System", ParseLogLevel(loggingOptions.SystemMinimumLevel));

            // Exclude noisy sources
            loggerConfiguration.MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning);
            loggerConfiguration.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning);

            // Configure console output
            if (loggingOptions.EnableConsoleLogging)
            {
                if (loggingOptions.EnableStructuredLogging)
                {
                    loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
                }
                else
                {
                    loggerConfiguration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
                }
            }

            // Configure file output
            if (loggingOptions.EnableFileLogging)
            {
                var rollingInterval = Enum.Parse<RollingInterval>(loggingOptions.RollingInterval, true);
                
                loggerConfiguration.WriteTo.File(
                    path: loggingOptions.FileLoggingPath,
                    rollingInterval: rollingInterval,
                    retainedFileCountLimit: loggingOptions.RetainedFileCountLimit,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1));

                // Add structured JSON file for analysis
                if (loggingOptions.EnableStructuredLogging)
                {
                    var jsonPath = loggingOptions.FileLoggingPath.Replace(".txt", ".json");
                    loggerConfiguration.WriteTo.File(
                        formatter: new CompactJsonFormatter(),
                        path: jsonPath,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: loggingOptions.RetainedFileCountLimit,
                        shared: true);
                }
            }

            // Configure Application Insights
            if (!string.IsNullOrWhiteSpace(loggingOptions.ApplicationInsightsConnectionString) ||
                !string.IsNullOrWhiteSpace(loggingOptions.ApplicationInsightsInstrumentationKey))
            {
                var telemetryConfiguration = serviceProvider.GetService<TelemetryConfiguration>();
                
                if (telemetryConfiguration != null)
                {
                    loggerConfiguration.WriteTo.ApplicationInsights(
                        telemetryConfiguration,
                        TelemetryConverter.Traces,
                        restrictedToMinimumLevel: LogEventLevel.Information);
                }
                else
                {
                    // Fallback to connection string or instrumentation key
                    var connectionString = loggingOptions.ApplicationInsightsConnectionString ?? 
                        $"InstrumentationKey={loggingOptions.ApplicationInsightsInstrumentationKey}";
                    
                    loggerConfiguration.WriteTo.ApplicationInsights(
                        connectionString,
                        TelemetryConverter.Traces,
                        restrictedToMinimumLevel: LogEventLevel.Information);
                }
            }

            return loggerConfiguration;
        }

        /// <summary>
        /// Parses log level string to LogEventLevel enum
        /// </summary>
        private static LogEventLevel ParseLogLevel(string level)
        {
            return level?.ToUpperInvariant() switch
            {
                "VERBOSE" => LogEventLevel.Verbose,
                "DEBUG" => LogEventLevel.Debug,
                "INFORMATION" => LogEventLevel.Information,
                "WARNING" => LogEventLevel.Warning,
                "ERROR" => LogEventLevel.Error,
                "FATAL" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }
    }
}