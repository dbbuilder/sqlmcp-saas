using System;
using System.Collections.Generic;
using System.Linq;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Core.Models.Errors
{
    /// <summary>
    /// Standard error response model for API responses.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Gets or sets the correlation ID for tracking the error.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the collection of detailed errors.
        /// </summary>
        public List<ErrorDetail> Errors { get; set; }

        /// <summary>
        /// Initializes a new instance of the ErrorResponse class.
        /// </summary>
        public ErrorResponse()
        {
            Errors = new List<ErrorDetail>();
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the ErrorResponse class with specified values.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The error message.</param>
        public ErrorResponse(string correlationId, string message) : this()
        {
            CorrelationId = correlationId;
            Message = message;
        }

        /// <summary>
        /// Adds an error detail to the response.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="field">The field that caused the error (optional).</param>
        public void AddError(string code, string message, string field = null)
        {
            Errors.Add(new ErrorDetail(code, message, field));
        }

        /// <summary>
        /// Creates an ErrorResponse from an exception.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <returns>An ErrorResponse instance.</returns>
        public static ErrorResponse FromException(Exception exception)
        {
            if (exception is BaseException baseException)
            {
                var response = new ErrorResponse(baseException.CorrelationId, baseException.SafeMessage)
                {
                    Timestamp = baseException.Timestamp
                };

                // Add validation errors if it's a ValidationException
                if (baseException is ValidationException validationException && validationException.HasErrors)
                {
                    foreach (var fieldErrors in validationException.ValidationErrors)
                    {
                        foreach (var error in fieldErrors.Value)
                        {
                            response.AddError("VALIDATION_ERROR", error, fieldErrors.Key);
                        }
                    }
                }

                return response;
            }

            // For non-base exceptions, create a generic response
            return new ErrorResponse(
                Guid.NewGuid().ToString(),
                "An error has occurred. Please try again later.")
            {
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
