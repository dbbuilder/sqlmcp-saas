using System.Diagnostics;

namespace DatabaseAutomationPlatform.Api.Middleware;

/// <summary>
/// Middleware that ensures every request has a correlation ID for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        // Try to get correlation ID from request header
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) || 
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Add to response headers
        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        // Set in HttpContext items for use by other components
        context.Items["CorrelationId"] = correlationId.ToString();

        // Set Activity (for distributed tracing)
        using var activity = Activity.Current?.SetBaggage("CorrelationId", correlationId);

        // Add to logging context
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId.ToString()
        }))
        {
            _logger.LogInformation("Request started with correlation ID: {CorrelationId}", correlationId);
            
            try
            {
                await _next(context);
            }
            finally
            {
                _logger.LogInformation("Request completed with correlation ID: {CorrelationId}", correlationId);
            }
        }
    }
}