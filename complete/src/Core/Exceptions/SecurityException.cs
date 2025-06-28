using System;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown for security-related failures.
    /// Ensures no sensitive information is exposed in error messages.
    /// </summary>
    public class SecurityException : BaseException
    {
        /// <summary>
        /// Gets the type of security event that occurred.
        /// </summary>
        public SecurityEventType SecurityEventType { get; }

        /// <summary>
        /// Gets the user ID associated with the security event.
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Gets the resource that was attempted to be accessed.
        /// </summary>
        public string Resource { get; private set; }

        /// <summary>
        /// Initializes a new instance of the SecurityException class.
        /// </summary>
        /// <param name="message">The detailed error message for logging.</param>
        public SecurityException(string message) 
            : base(message, "Access denied. You do not have permission to perform this action.")
        {
            SecurityEventType = SecurityEventType.Unauthorized;
            AddDetail("SecurityEventType", SecurityEventType.ToString());
        }

        /// <summary>
        /// Initializes a new instance of the SecurityException class with a specific event type.
        /// </summary>
        /// <param name="message">The detailed error message for logging.</param>
        /// <param name="eventType">The type of security event.</param>
        public SecurityException(string message, SecurityEventType eventType) 
            : base(message, GetSafeMessageForEventType(eventType))
        {
            SecurityEventType = eventType;
            AddDetail("SecurityEventType", eventType.ToString());
        }

        /// <summary>
        /// Sets the user ID associated with this security event.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public SecurityException WithUserId(string userId)
        {
            UserId = userId;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                AddDetail("UserId", userId);
            }
            return this;
        }

        /// <summary>
        /// Sets the resource that was attempted to be accessed.
        /// </summary>
        /// <param name="resource">The resource identifier.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public SecurityException WithResource(string resource)
        {
            Resource = resource;
            if (!string.IsNullOrWhiteSpace(resource))
            {
                AddDetail("Resource", resource);
            }
            return this;
        }

        /// <summary>
        /// Sets the IP address associated with this security event.
        /// </summary>
        /// <param name="ipAddress">The IP address.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public SecurityException WithIpAddress(string ipAddress)
        {
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                AddDetail("IpAddress", ipAddress);
            }
            return this;
        }

        /// <summary>
        /// Gets the appropriate safe message for the security event type.
        /// </summary>
        private static string GetSafeMessageForEventType(SecurityEventType eventType)
        {
            return eventType switch
            {
                SecurityEventType.AuthenticationFailure => "Authentication failed. Please check your credentials and try again.",
                SecurityEventType.AuthorizationFailure => "You are not authorized to access this resource.",
                SecurityEventType.SuspiciousActivity => "Your request has been blocked for security reasons.",
                SecurityEventType.TokenExpired => "Your session has expired. Please sign in again.",
                SecurityEventType.InvalidToken => "Invalid authentication token. Please sign in again.",
                _ => "Access denied. You do not have permission to perform this action."
            };
        }
    }
}
