using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using DatabaseAutomationPlatform.Infrastructure.Logging.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Logging.Middleware;

namespace DatabaseAutomationPlatform.Infrastructure.Logging
{
    /// <summary>
    /// Extension methods for configuring logging services
    /// </summary>
    public static class LoggingServiceExtensions
    {
        /// <summary>
        /// Adds logging services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddLoggingServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add logging options
            services.Configure<LoggingOptions>(configuration.GetSection(LoggingOptions.SectionName));

            // Add HTTP context accessor for correlation ID
            services.AddHttpContextAccessor();

            // Add Application Insights
            var loggingOptions = configuration.GetSection(LoggingOptions.SectionName)
                .Get<LoggingOptions>() ?? new LoggingOptions();

            if (!string.IsNullOrWhiteSpace(loggingOptions.ApplicationInsightsConnectionString) ||
                !string.IsNullOrWhiteSpace(loggingOptions.ApplicationInsightsInstrumentationKey))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    if (!string.IsNullOrWhiteSpace(loggingOptions.ApplicationInsightsConnectionString))
                    {
                        options.ConnectionString = loggingOptions.ApplicationInsightsConnectionString;
                    }
                    else
                    {
                        options.InstrumentationKey = loggingOptions.ApplicationInsightsInstrumentationKey;
                    }
                    
                    options.EnableRequestTrackingTelemetryModule = true;
                    options.EnableDependencyTrackingTelemetryModule = true;
                    options.EnablePerformanceCounterCollectionModule = true;
                    options.EnableEventCounterCollectionModule = true;
                    options.EnableDiagnosticsTelemetryModule = true;
                    options.EnableDebugLogger = false;
                });

                // Configure dependency tracking
                services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
                {
                    module.EnableSqlCommandTextInstrumentation = loggingOptions.EnableSensitiveDataLogging;
                });

                // Add custom telemetry initializers
                services.AddSingleton<ITelemetryInitializer, CorrelationIdTelemetryInitializer>();
            }

            return services;
        }

        /// <summary>
        /// Configures Serilog for the host
        /// </summary>
        /// <param name="hostBuilder">The host builder</param>
        /// <returns>The host builder for chaining</returns>
        public static IHostBuilder ConfigureSerilog(this IHostBuilder hostBuilder)
        {
            return hostBuilder.UseSerilog((context, services, configuration) =>
            {
                var loggerConfig = SerilogConfiguration.ConfigureSerilog(
                    context.Configuration,
                    services);
                    
                configuration = loggerConfig;
            });
        }

        /// <summary>
        /// Adds the logging middleware to the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseLoggingMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LoggingMiddleware>();
        }

        /// <summary>
        /// Configures request logging with Serilog
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                
                // Enhance the request log with additional properties
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("RemoteIPAddress", httpContext.Connection.RemoteIpAddress?.ToString());
                    
                    if (httpContext.User?.Identity?.IsAuthenticated ?? false)
                    {
                        diagnosticContext.Set("UserName", httpContext.User.Identity.Name);
                    }
                };
            });
        }
    }

    /// <summary>
    /// Telemetry initializer for adding correlation ID to Application Insights
    /// </summary>
    public class CorrelationIdTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public void Initialize(ITelemetry telemetry)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                // Add correlation ID
                if (context.Items.TryGetValue("CorrelationId", out var correlationId) && 
                    correlationId is string correlationIdString)
                {
                    telemetry.Context.GlobalProperties["CorrelationId"] = correlationIdString;
                }

                // Add user ID if authenticated
                if (context.User?.Identity?.IsAuthenticated ?? false)
                {
                    telemetry.Context.User.AuthenticatedUserId = context.User.Identity.Name;
                }
            }
        }
    }
}