namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Defines types of security events for categorizing security exceptions.
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// General unauthorized access attempt.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Authentication failure (invalid credentials).
        /// </summary>
        AuthenticationFailure,

        /// <summary>
        /// Authorization failure (valid user, insufficient permissions).
        /// </summary>
        AuthorizationFailure,

        /// <summary>
        /// Suspicious activity detected (potential attack).
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// Token or session expired.
        /// </summary>
        TokenExpired,

        /// <summary>
        /// Invalid or malformed token.
        /// </summary>
        InvalidToken
    }
}
