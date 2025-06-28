using DatabaseAutomationPlatform.Api.Models;
using DatabaseAutomationPlatform.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace DatabaseAutomationPlatform.Api.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions and returns a consistent error response
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
        
        // Log the exception
        _logger.LogError(exception, 
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}", 
            correlationId, context.Request.Path, context.Request.Method);

        // Determine status code and error response
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Validation Error",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = validationEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId,
                    Errors = validationEx.Errors
                }),
            
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Resource Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = notFoundEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            UnauthorizedException unauthorizedEx => (
                HttpStatusCode.Unauthorized,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = (int)HttpStatusCode.Unauthorized,
                    Detail = unauthorizedEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            ForbiddenException forbiddenEx => (
                HttpStatusCode.Forbidden,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = (int)HttpStatusCode.Forbidden,
                    Detail = forbiddenEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                    Title = "Conflict",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = conflictEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            BusinessRuleException businessEx => (
                HttpStatusCode.UnprocessableEntity,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                    Title = "Business Rule Violation",
                    Status = 422,
                    Detail = businessEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            ExternalServiceException externalEx => (
                HttpStatusCode.BadGateway,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.3",
                    Title = "External Service Error",
                    Status = (int)HttpStatusCode.BadGateway,
                    Detail = externalEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            TooManyRequestsException rateLimitEx => (
                HttpStatusCode.TooManyRequests,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = 429,
                    Detail = rateLimitEx.UserMessage,
                    Instance = context.Request.Path,
                    CorrelationId = correlationId,
                    Extensions = new Dictionary<string, object>
                    {
                        ["retryAfter"] = rateLimitEx.RetryAfterSeconds
                    }
                }),
            
            DatabaseException dbEx => (
                HttpStatusCode.ServiceUnavailable,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    Title = "Database Error",
                    Status = (int)HttpStatusCode.ServiceUnavailable,
                    Detail = "A database error occurred. Please try again later.",
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                }),
            
            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Internal Server Error",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = _environment.IsDevelopment() 
                        ? exception.Message 
                        : "An unexpected error occurred. Please try again later.",
                    Instance = context.Request.Path,
                    CorrelationId = correlationId
                })
        };

        // Add stack trace in development
        if (_environment.IsDevelopment() && errorResponse.Extensions == null)
        {
            errorResponse.Extensions = new Dictionary<string, object>
            {
                ["stackTrace"] = exception.StackTrace ?? string.Empty
            };
        }

        // Add retry-after header for rate limit exceptions
        if (exception is TooManyRequestsException tooManyEx)
        {
            context.Response.Headers.Append("Retry-After", tooManyEx.RetryAfterSeconds.ToString());
        }

        // Write response
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}