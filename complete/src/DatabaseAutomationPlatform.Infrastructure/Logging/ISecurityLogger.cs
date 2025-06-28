namespace DatabaseAutomationPlatform.Infrastructure.Logging
{
    /// <summary>
    /// Interface for logging security-related events
    /// </summary>
    public interface ISecurityLogger
    {
        /// <summary>
        /// Logs a security event asynchronously
        /// </summary>
        /// <param name="securityEvent">The security event to log</param>
        /// <returns>Task representing the async operation</returns>
        Task LogSecurityEventAsync(SecurityEvent securityEvent);
    }

    /// <summary>
    /// Represents a security event to be logged
    /// </summary>
    public class SecurityEvent
    {
        /// <summary>
        /// Type of security event
        /// </summary>
        public SecurityEventType EventType { get; set; }
        
        /// <summary>
        /// User ID associated with the event
        /// </summary>
        public string UserId { get; set; } = "System";
        
        /// <summary>
        /// Resource name being accessed
        /// </summary>
        public string ResourceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// IP address of the client
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }        
        /// <summary>
        /// Additional details about the event
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
        
        /// <summary>
        /// Timestamp of the event (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Types of security events
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>Database connection attempt</summary>
        DatabaseConnection,
        
        /// <summary>Authentication attempt</summary>
        Authentication,
        
        /// <summary>Authorization check</summary>
        Authorization,
        
        /// <summary>Data access operation</summary>
        DataAccess,
        
        /// <summary>Configuration change</summary>
        ConfigurationChange,
        
        /// <summary>Security exception occurred</summary>
        SecurityException,
        
        /// <summary>Audit log access</summary>
        AuditLogAccess,
        
        /// <summary>Key vault access</summary>
        KeyVaultAccess,
        
        /// <summary>Failed operation</summary>
        OperationFailed
    }
}