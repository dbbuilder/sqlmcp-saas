using System;
using System.Collections.Generic;
using System.Text;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Base exception class for all custom exceptions in the SQL MCP system.
    /// Provides correlation ID tracking, safe error messages, and detailed logging capabilities.
    /// </summary>
    public class BaseException : Exception
    {
        private const string DefaultSafeMessage = "An error has occurred. Please try again later.";

        /// <summary>
        /// Gets the correlation ID for tracking the error across the system.
        /// </summary>
        public string CorrelationId { get; private set; }

        /// <summary>
        /// Gets the UTC timestamp when the exception occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the safe error message that can be shown to end users.
        /// </summary>
        public string SafeMessage { get; }

        /// <summary>
        /// Gets additional details about the error for logging purposes.
        /// </summary>
        public Dictionary<string, string> Details { get; }

        /// <summary>
        /// Initializes a new instance of the BaseException class.
        /// </summary>
        /// <param name="message">The detailed error message for logging.</param>
        /// <param name="safeMessage">The safe message to show to users.</param>
        public BaseException(string message, string safeMessage) 
            : base(message)
        {
            CorrelationId = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
            SafeMessage = string.IsNullOrWhiteSpace(safeMessage) ? DefaultSafeMessage : safeMessage;
            Details = new Dictionary<string, string>();
        }

        /// <summary>
        /// Initializes a new instance of the BaseException class with an inner exception.
        /// </summary>
        /// <param name="message">The detailed error message for logging.</param>
        /// <param name="safeMessage">The safe message to show to users.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public BaseException(string message, string safeMessage, Exception innerException) 
            : base(message, innerException)
        {
            CorrelationId = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
            SafeMessage = string.IsNullOrWhiteSpace(safeMessage) ? DefaultSafeMessage : safeMessage;
            Details = new Dictionary<string, string>();
        }

        /// <summary>
        /// Adds additional detail information to the exception for logging purposes.
        /// </summary>
        /// <param name="key">The detail key.</param>
        /// <param name="value">The detail value.</param>
        public void AddDetail(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            Details[key] = value;
        }

        /// <summary>
        /// Sets a specific correlation ID for this exception.
        /// </summary>
        /// <param name="correlationId">The correlation ID to set.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public BaseException WithCorrelationId(string correlationId)
        {
            CorrelationId = correlationId;
            return this;
        }

        /// <summary>
        /// Gets a detailed log message including all exception information.
        /// This should only be used for internal logging, never exposed to users.
        /// </summary>
        /// <returns>A detailed log message.</returns>
        public string GetLogMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{CorrelationId}] Exception occurred at {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
            sb.AppendLine($"Type: {GetType().FullName}");
            sb.AppendLine($"Message: {Message}");
            
            if (Details.Count > 0)
            {
                sb.AppendLine("Details:");
                foreach (var detail in Details)
                {
                    sb.AppendLine($"  {detail.Key}: {detail.Value}");
                }
            }

            if (InnerException != null)
            {
                sb.AppendLine($"Inner Exception: {InnerException.GetType().FullName}");
                sb.AppendLine($"Inner Message: {InnerException.Message}");
            }

            if (!string.IsNullOrWhiteSpace(StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(StackTrace);
            }

            return sb.ToString();
        }
    }
}
