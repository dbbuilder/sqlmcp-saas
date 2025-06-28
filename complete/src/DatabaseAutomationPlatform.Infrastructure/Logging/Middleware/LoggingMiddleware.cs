using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace DatabaseAutomationPlatform.Infrastructure.Logging.Middleware
{
    /// <summary>
    /// Middleware for logging HTTP requests and responses
    /// </summary>
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly LoggingOptions _options;
        private readonly ISecurityLogger _securityLogger;

        // Sensitive headers that should not be logged
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-Api-Key",
            "X-Auth-Token"
        };

        /// <summary>
        /// Initializes a new instance of the LoggingMiddleware class
        /// </summary>        public LoggingMiddleware(
            RequestDelegate next,
            ILogger<LoggingMiddleware> logger,
            IOptions<LoggingOptions> options,
            ISecurityLogger securityLogger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.EnableRequestResponseLogging)
            {
                await _next(context);
                return;
            }

            // Generate and set correlation ID
            var correlationId = GetOrCreateCorrelationId(context);
            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            // Start timing
            var stopwatch = Stopwatch.StartNew();
            // Log request
            await LogRequest(context, correlationId);

            // Capture original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                // Continue down the pipeline
                await _next(context);

                // Log response
                await LogResponse(context, correlationId, stopwatch.ElapsedMilliseconds);

                // Copy the response body back to the original stream
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Log error
                _logger.LogError(ex,
                    "Request {Method} {Path} failed after {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                // Log security event for failed requests
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.OperationFailed,
                    UserId = context.User?.Identity?.Name ?? "Anonymous",
                    ResourceName = $"{context.Request.Method} {context.Request.Path}",
                    Success = false,
                    IpAddress = GetClientIpAddress(context),
                    ErrorMessage = "Request processing failed",
                    CorrelationId = correlationId,
                    Details = new Dictionary<string, object>
                    {
                        ["Method"] = context.Request.Method,
                        ["Path"] = context.Request.Path.ToString(),
                        ["StatusCode"] = context.Response.StatusCode,
                        ["ElapsedMs"] = stopwatch.ElapsedMilliseconds,
                        ["ExceptionType"] = ex.GetType().Name
                    }
                });

                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
        /// <summary>
        /// Logs the incoming request
        /// </summary>
        private async Task LogRequest(HttpContext context, string correlationId)
        {
            context.Request.EnableBuffering();

            var requestInfo = new StringBuilder();
            requestInfo.AppendLine($"HTTP Request Information:");
            requestInfo.AppendLine($"Method: {context.Request.Method}");
            requestInfo.AppendLine($"Path: {context.Request.Path}");
            requestInfo.AppendLine($"QueryString: {context.Request.QueryString}");
            requestInfo.AppendLine($"CorrelationId: {correlationId}");

            // Log headers (excluding sensitive ones)
            if (_options.EnableStructuredLogging)
            {
                var headers = context.Request.Headers
                    .Where(h => !SensitiveHeaders.Contains(h.Key))
                    .ToDictionary(h => h.Key, h => h.Value.ToString());

                _logger.LogInformation("Request started {Method} {Path} with headers {@Headers}",
                    context.Request.Method,
                    context.Request.Path,
                    headers);
            }
            // Log body if enabled and within size limit
            if (context.Request.ContentLength > 0 && context.Request.ContentLength <= _options.MaxRequestBodySize)
            {
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(body) && !_options.EnableSensitiveDataLogging)
                {
                    // Mask potential sensitive data in body
                    body = MaskSensitiveData(body);
                }

                requestInfo.AppendLine($"Body: {body}");
            }

            _logger.LogInformation(requestInfo.ToString());
        }

        /// <summary>
        /// Logs the outgoing response
        /// </summary>
        private async Task LogResponse(HttpContext context, string correlationId, long elapsedMs)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var logLevel = context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel,
                "Request {Method} {Path} responded {StatusCode} in {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsedMs,
                correlationId);

            // Log performance warning if request took too long
            if (_options.EnablePerformanceLogging && elapsedMs > _options.PerformanceLoggingThreshold)
            {
                _logger.LogWarning(
                    "Slow request detected: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms). CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    elapsedMs,
                    _options.PerformanceLoggingThreshold,
                    correlationId);
            }

            // Log response body for non-success status codes
            if (context.Response.StatusCode >= 400 && !string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.LogWarning("Error response body: {ResponseBody}", responseBody);
            }
        }
        /// <summary>
        /// Gets or creates a correlation ID for the request
        /// </summary>
        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            // Try to get from request header
            if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            {
                return correlationId.ToString();
            }

            // Generate new one
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Gets the client IP address from the request
        /// </summary>
        private static string GetClientIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            // Check X-Real-IP header
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // Fall back to remote IP address
            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Masks sensitive data in request/response bodies
        /// </summary>
        private static string MaskSensitiveData(string data)
        {
            // Simple masking for common patterns
            // In production, use more sophisticated masking

            // Mask potential passwords
            data = System.Text.RegularExpressions.Regex.Replace(
                data,
                @"(""password""\s*:\s*"")[^""]+("")",
                "$1*****$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Mask potential API keys
            data = System.Text.RegularExpressions.Regex.Replace(
                data,
                @"(""api[_-]?key""\s*:\s*"")[^""]+("")",
                "$1*****$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Mask potential tokens
            data = System.Text.RegularExpressions.Regex.Replace(
                data,
                @"(""token""\s*:\s*"")[^""]+("")",
                "$1*****$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return data;
        }
    }
}