using System.Diagnostics;
using System.Text;

namespace DatabaseAutomationPlatform.Api.Middleware;

/// <summary>
/// Middleware that logs HTTP request and response details for auditing and debugging
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key"
    };

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "N/A";

        // Log request
        await LogRequest(context, correlationId);

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Log response
            await LogResponse(context, correlationId, stopwatch.ElapsedMilliseconds);

            // Copy the response body back to the original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            stopwatch.Stop();
        }
    }

    private async Task LogRequest(HttpContext context, string correlationId)
    {
        var request = context.Request;
        
        // Enable buffering to allow multiple reads of the request body
        context.Request.EnableBuffering();

        var requestLog = new StringBuilder();
        requestLog.AppendLine($"HTTP Request Information:");
        requestLog.AppendLine($"Correlation ID: {correlationId}");
        requestLog.AppendLine($"Schema: {request.Scheme}");
        requestLog.AppendLine($"Host: {request.Host}");
        requestLog.AppendLine($"Path: {request.Path}");
        requestLog.AppendLine($"QueryString: {request.QueryString}");
        requestLog.AppendLine($"Method: {request.Method}");
        requestLog.AppendLine($"User: {context.User?.Identity?.Name ?? "Anonymous"}");
        requestLog.AppendLine($"Client IP: {context.Connection.RemoteIpAddress}");

        // Log headers (excluding sensitive ones)
        requestLog.AppendLine("Headers:");
        foreach (var header in request.Headers)
        {
            if (_sensitiveHeaders.Contains(header.Key))
            {
                requestLog.AppendLine($"  {header.Key}: [REDACTED]");
            }
            else
            {
                requestLog.AppendLine($"  {header.Key}: {header.Value}");
            }
        }

        // Log request body for non-GET requests
        if (request.Method != HttpMethods.Get && request.ContentLength > 0)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                // Truncate large bodies
                var bodyToLog = body.Length > 1000 ? body.Substring(0, 1000) + "... [TRUNCATED]" : body;
                requestLog.AppendLine($"Body: {bodyToLog}");
            }
        }

        _logger.LogInformation(requestLog.ToString());
    }

    private async Task LogResponse(HttpContext context, string correlationId, long elapsedMs)
    {
        var response = context.Response;
        
        var responseLog = new StringBuilder();
        responseLog.AppendLine($"HTTP Response Information:");
        responseLog.AppendLine($"Correlation ID: {correlationId}");
        responseLog.AppendLine($"Status Code: {response.StatusCode}");
        responseLog.AppendLine($"Elapsed Time: {elapsedMs}ms");

        // Log response headers (excluding sensitive ones)
        responseLog.AppendLine("Headers:");
        foreach (var header in response.Headers)
        {
            if (_sensitiveHeaders.Contains(header.Key))
            {
                responseLog.AppendLine($"  {header.Key}: [REDACTED]");
            }
            else
            {
                responseLog.AppendLine($"  {header.Key}: {header.Value}");
            }
        }

        // Only log response body for error responses
        if (response.StatusCode >= 400)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var bodyToLog = body.Length > 1000 ? body.Substring(0, 1000) + "... [TRUNCATED]" : body;
                responseLog.AppendLine($"Body: {bodyToLog}");
            }
        }

        if (response.StatusCode >= 500)
        {
            _logger.LogError(responseLog.ToString());
        }
        else if (response.StatusCode >= 400)
        {
            _logger.LogWarning(responseLog.ToString());
        }
        else
        {
            _logger.LogInformation(responseLog.ToString());
        }
    }
}