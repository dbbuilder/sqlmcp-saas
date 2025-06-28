using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SqlMcp.Core.Models.Errors
{
    /// <summary>
    /// RFC 7807 compliant problem details response.
    /// See: https://tools.ietf.org/html/rfc7807
    /// </summary>
    public class ProblemDetailsResponse
    {
        /// <summary>
        /// Gets or sets a URI reference that identifies the problem type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a short, human-readable summary of the problem type.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        [JsonPropertyName("status")]
        public int? Status { get; set; }

        /// <summary>
        /// Gets or sets a human-readable explanation specific to this occurrence of the problem.
        /// </summary>
        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        /// <summary>
        /// Gets or sets a URI reference that identifies the specific occurrence of the problem.
        /// </summary>
        [JsonPropertyName("instance")]
        public string Instance { get; set; }

        /// <summary>
        /// Gets or sets extension members.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Extensions { get; set; }

        /// <summary>
        /// Initializes a new instance of the ProblemDetailsResponse class.
        /// </summary>
        public ProblemDetailsResponse()
        {
            Extensions = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a ProblemDetailsResponse from an exception.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="requestPath">The request path.</param>
        /// <returns>A ProblemDetailsResponse instance.</returns>
        public static ProblemDetailsResponse FromException(Exception exception, int statusCode, string requestPath = null)
        {
            var problemDetails = new ProblemDetailsResponse
            {
                Status = statusCode,
                Type = GetProblemType(exception, statusCode),
                Title = GetProblemTitle(exception, statusCode),
                Instance = requestPath
            };

            if (exception is BaseException baseException)
            {
                problemDetails.Detail = baseException.SafeMessage;
                problemDetails.Extensions["correlationId"] = baseException.CorrelationId;
                problemDetails.Extensions["timestamp"] = baseException.Timestamp;

                if (exception is ValidationException validationException && validationException.HasErrors)
                {
                    problemDetails.Extensions["errors"] = validationException.ValidationErrors;
                }
            }
            else
            {
                problemDetails.Detail = "An error has occurred. Please try again later.";
                problemDetails.Extensions["correlationId"] = Guid.NewGuid().ToString();
                problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
            }

            return problemDetails;
        }

        private static string GetProblemType(Exception exception, int statusCode)
        {
            return exception switch
            {
                ValidationException => "https://sqlmcp.api/problems/validation-error",
                ResourceNotFoundException => "https://sqlmcp.api/problems/not-found",
                SecurityException => "https://sqlmcp.api/problems/security-error",
                BusinessRuleException => "https://sqlmcp.api/problems/business-rule-violation",
                DatabaseException => "https://sqlmcp.api/problems/database-error",
                _ => $"https://sqlmcp.api/problems/error/{statusCode}"
            };
        }

        private static string GetProblemTitle(Exception exception, int statusCode)
        {
            return exception switch
            {
                ValidationException => "Validation Error",
                ResourceNotFoundException => "Resource Not Found",
                SecurityException => "Security Error",
                BusinessRuleException => "Business Rule Violation",
                DatabaseException => "Database Error",
                _ => statusCode switch
                {
                    400 => "Bad Request",
                    401 => "Unauthorized",
                    403 => "Forbidden",
                    404 => "Not Found",
                    500 => "Internal Server Error",
                    503 => "Service Unavailable",
                    _ => "Error"
                }
            };
        }
    }
}
