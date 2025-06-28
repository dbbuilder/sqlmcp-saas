using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DatabaseAutomationPlatform.Infrastructure.Logging
{
    /// <summary>
    /// Implementation of security logger that writes to structured logging
    /// </summary>
    public class SecurityLogger : ISecurityLogger
    {
        private readonly ILogger<SecurityLogger> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public SecurityLogger(ILogger<SecurityLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task LogSecurityEventAsync(SecurityEvent securityEvent)
        {
            if (securityEvent == null)
                throw new ArgumentNullException(nameof(securityEvent));

            var logLevel = DetermineLogLevel(securityEvent);
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = securityEvent.CorrelationId,
                ["EventType"] = securityEvent.EventType.ToString(),
                ["UserId"] = securityEvent.UserId,
                ["Success"] = securityEvent.Success
            }))
            {
                _logger.Log(
                    logLevel,
                    "Security Event: {EventType} for {ResourceName} by {UserId} from {IpAddress} - Success: {Success}",
                    securityEvent.EventType,
                    securityEvent.ResourceName,
                    securityEvent.UserId,
                    securityEvent.IpAddress,
                    securityEvent.Success);

                // Log additional details if present
                if (securityEvent.Details?.Count > 0)
                {
                    var detailsJson = JsonSerializer.Serialize(securityEvent.Details, JsonOptions);
                    _logger.LogDebug("Security Event Details: {Details}", detailsJson);
                }
                // Log error message if present
                if (!string.IsNullOrWhiteSpace(securityEvent.ErrorMessage))
                {
                    _logger.LogError("Security Event Error: {ErrorMessage}", securityEvent.ErrorMessage);
                }
            }

            // For critical security events, ensure they're written immediately
            if (IsCriticalEvent(securityEvent))
            {
                // In a production system, this might also send to a SIEM
                _logger.LogCritical(
                    "CRITICAL SECURITY EVENT: {EventType} - {ResourceName} - Success: {Success}",
                    securityEvent.EventType,
                    securityEvent.ResourceName,
                    securityEvent.Success);
            }

            return Task.CompletedTask;
        }

        private static LogLevel DetermineLogLevel(SecurityEvent securityEvent)
        {
            return (securityEvent.EventType, securityEvent.Success) switch
            {
                (SecurityEventType.SecurityException, _) => LogLevel.Error,
                (SecurityEventType.Authentication, false) => LogLevel.Warning,
                (SecurityEventType.Authorization, false) => LogLevel.Warning,
                (SecurityEventType.DatabaseConnection, false) => LogLevel.Warning,
                (SecurityEventType.KeyVaultAccess, false) => LogLevel.Error,
                (_, false) => LogLevel.Warning,
                _ => LogLevel.Information
            };
        }

        private static bool IsCriticalEvent(SecurityEvent securityEvent)
        {
            return securityEvent.EventType switch
            {
                SecurityEventType.SecurityException => true,
                SecurityEventType.Authentication when !securityEvent.Success => true,
                SecurityEventType.KeyVaultAccess when !securityEvent.Success => true,
                _ => false
            };
        }
    }
}