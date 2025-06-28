using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SqlMcp.Core.Exceptions;
using SqlMcp.Core.Models.Errors;

namespace SqlMcp.Core.ErrorHandling
{
    /// <summary>
    /// Global exception handling middleware that catches all unhandled exceptions
    /// and returns appropriate error responses.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the GlobalExceptionMiddleware class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Invokes the middleware to handle the request.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Handles the exception and writes an appropriate response.
        /// </summary>
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Don't log OperationCanceledException (client disconnects)
            if (exception is not OperationCanceledException)
            {
                LogException(exception);
            }

            var (statusCode, errorResponse) = GetErrorResponse(exception);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(errorResponse, _jsonOptions));
        }

        /// <summary>
        /// Maps exceptions to appropriate HTTP status codes and error responses.
        /// </summary>
        private (HttpStatusCode statusCode, ErrorResponse response) GetErrorResponse(Exception exception)
        {
            var errorResponse = ErrorResponse.FromException(exception);

            var statusCode = exception switch
            {
                ValidationException => HttpStatusCode.BadRequest,
                ResourceNotFoundException => HttpStatusCode.NotFound,
                SecurityException secEx => GetSecurityStatusCode(secEx),
                BusinessRuleException => HttpStatusCode.UnprocessableEntity,
                ConfigurationException => HttpStatusCode.InternalServerError,
                DatabaseException => HttpStatusCode.ServiceUnavailable,
                OperationCanceledException => HttpStatusCode.RequestTimeout,
                _ => HttpStatusCode.InternalServerError
            };

            return (statusCode, errorResponse);
        }

        /// <summary>
        /// Gets the appropriate status code for security exceptions.
        /// </summary>
        private HttpStatusCode GetSecurityStatusCode(SecurityException exception)
        {
            return exception.SecurityEventType switch
            {
                SecurityEventType.AuthenticationFailure => HttpStatusCode.Unauthorized,
                SecurityEventType.TokenExpired => HttpStatusCode.Unauthorized,
                SecurityEventType.InvalidToken => HttpStatusCode.Unauthorized,
                SecurityEventType.AuthorizationFailure => HttpStatusCode.Forbidden,
                SecurityEventType.SuspiciousActivity => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.Forbidden
            };
        }

        /// <summary>
        /// Logs the exception with appropriate detail level.
        /// </summary>
        private void LogException(Exception exception)
        {
            if (exception is BaseException baseException)
            {
                _logger.LogError(exception, 
                    "Handled exception occurred. CorrelationId: {CorrelationId}, Type: {ExceptionType}, Details: {LogMessage}",
                    baseException.CorrelationId,
                    exception.GetType().Name,
                    baseException.GetLogMessage());
            }
            else
            {
                _logger.LogError(exception,
                    "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}",
                    exception.GetType().Name,
                    exception.Message);
            }
        }
    }

    /// <summary>
    /// Extension methods for registering the exception middleware.
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Adds the global exception handling middleware to the pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
