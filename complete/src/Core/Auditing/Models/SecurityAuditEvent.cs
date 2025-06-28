using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlMcp.Core.Auditing.Models
{
    /// <summary>
    /// Types of security events that can be audited
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// Successful login
        /// </summary>
        LoginSuccess,

        /// <summary>
        /// Failed login attempt
        /// </summary>
        LoginFailure,

        /// <summary>
        /// Successful logout
        /// </summary>
        Logout,

        /// <summary>
        /// Unauthorized access attempt
        /// </summary>
        UnauthorizedAccess,

        /// <summary>
        /// Permission denied
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// Token expired
        /// </summary>
        TokenExpired,

        /// <summary>
        /// Token refreshed
        /// </summary>
        TokenRefreshed,

        /// <summary>
        /// Password changed
        /// </summary>
        PasswordChanged,

        /// <summary>
        /// Password reset requested
        /// </summary>
        PasswordResetRequested,

        /// <summary>
        /// Account locked due to failed attempts
        /// </summary>
        AccountLocked,

        /// <summary>
        /// Account unlocked
        /// </summary>
        AccountUnlocked,

        /// <summary>
        /// Suspicious activity detected
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// Role assignment changed
        /// </summary>
        RoleChanged,

        /// <summary>
        /// Permission grant
        /// </summary>
        PermissionGranted,

        /// <summary>
        /// Permission revoked
        /// </summary>
        PermissionRevoked
    }

    /// <summary>
    /// Audit event for security-related operations
    /// </summary>
    public class SecurityAuditEvent : AuditEvent
    {
        private double? _riskScore;

        /// <summary>
        /// Type of security event
        /// </summary>
        public SecurityEventType SecurityEventType { get; set; }

        /// <summary>
        /// Resource being accessed
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// Authentication method used
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// Authentication provider
        /// </summary>
        public string AuthenticationProvider { get; set; }

        /// <summary>
        /// Reason for the security event
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Required permissions for the resource
        /// </summary>
        public List<string> RequiredPermissions { get; set; }

        /// <summary>
        /// User's actual permissions
        /// </summary>
        public List<string> UserPermissions { get; set; }

        /// <summary>
        /// Risk score (0.0 to 1.0)
        /// </summary>
        public double? RiskScore
        {
            get => _riskScore;
            set
            {
                if (value.HasValue && (value < 0.0 || value > 1.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), 
                        "Risk score must be between 0.0 and 1.0");
                }
                _riskScore = value;
            }
        }

        /// <summary>
        /// Threat indicators
        /// </summary>
        public Dictionary<string, object> ThreatIndicators { get; set; }

        /// <summary>
        /// User roles at time of event
        /// </summary>
        public List<string> UserRoles { get; set; }

        /// <summary>
        /// Creates a new security audit event
        /// </summary>
        public SecurityAuditEvent(
            SecurityEventType securityEventType,
            string userId,
            string resource,
            string correlationId = null)
            : base($"Security.{securityEventType}", userId, correlationId)
        {
            SecurityEventType = securityEventType;
            Resource = resource;

            // Set severity based on event type
            Severity = securityEventType switch
            {
                SecurityEventType.LoginSuccess => AuditSeverity.Information,
                SecurityEventType.Logout => AuditSeverity.Information,
                SecurityEventType.TokenExpired => AuditSeverity.Information,
                SecurityEventType.TokenRefreshed => AuditSeverity.Information,
                SecurityEventType.PasswordChanged => AuditSeverity.Information,
                SecurityEventType.LoginFailure => AuditSeverity.Warning,
                SecurityEventType.UnauthorizedAccess => AuditSeverity.Warning,
                SecurityEventType.PermissionDenied => AuditSeverity.Warning,
                SecurityEventType.AccountLocked => AuditSeverity.Warning,
                SecurityEventType.SuspiciousActivity => AuditSeverity.Critical,
                _ => AuditSeverity.Information
            };
        }

        /// <summary>
        /// Protected constructor for deserialization
        /// </summary>
        protected SecurityAuditEvent() : base()
        {
        }

        /// <summary>
        /// Get permissions that the user is missing
        /// </summary>
        public List<string> GetMissingPermissions()
        {
            if (RequiredPermissions == null || UserPermissions == null)
                return new List<string>();

            return RequiredPermissions
                .Except(UserPermissions)
                .ToList();
        }

        /// <inheritdoc/>
        public override string ToLogString()
        {
            var sb = new StringBuilder(base.ToLogString());

            sb.Append($", SecurityEvent={SecurityEventType}");
            
            if (!string.IsNullOrEmpty(Resource))
                sb.Append($", Resource={Resource}");

            if (!string.IsNullOrEmpty(AuthenticationMethod))
                sb.Append($", AuthMethod={AuthenticationMethod}");

            if (!string.IsNullOrEmpty(AuthenticationProvider))
                sb.Append($", AuthProvider={AuthenticationProvider}");

            if (!string.IsNullOrEmpty(Reason))
                sb.Append($", Reason={Reason}");

            var missingPerms = GetMissingPermissions();
            if (missingPerms.Any())
                sb.Append($", MissingPermissions={string.Join(",", missingPerms)}");

            if (RiskScore.HasValue)
                sb.Append($", RiskScore={RiskScore:F2}");

            if (ThreatIndicators?.Any() == true)
                sb.Append($", ThreatIndicators={ThreatIndicators.Count}");

            return sb.ToString();
        }

        /// <inheritdoc/>
        public override IAuditEvent Clone()
        {
            var clone = new SecurityAuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = EventType,
                Timestamp = Timestamp,
                UserId = UserId,
                CorrelationId = CorrelationId,
                SessionId = SessionId,
                IpAddress = IpAddress,
                MachineName = MachineName,
                ApplicationName = ApplicationName,
                Severity = Severity,
                SecurityEventType = SecurityEventType,
                Resource = Resource,
                AuthenticationMethod = AuthenticationMethod,
                AuthenticationProvider = AuthenticationProvider,
                Reason = Reason,
                RiskScore = RiskScore
            };

            // Deep copy collections
            if (AdditionalData != null)
                clone.AdditionalData = new Dictionary<string, object>(AdditionalData);

            if (RequiredPermissions != null)
                clone.RequiredPermissions = new List<string>(RequiredPermissions);

            if (UserPermissions != null)
                clone.UserPermissions = new List<string>(UserPermissions);

            if (ThreatIndicators != null)
                clone.ThreatIndicators = new Dictionary<string, object>(ThreatIndicators);

            if (UserRoles != null)
                clone.UserRoles = new List<string>(UserRoles);

            return clone;
        }
    }
}
