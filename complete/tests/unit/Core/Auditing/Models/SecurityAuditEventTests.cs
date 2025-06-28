using System;
using System.Collections.Generic;
using SqlMcp.Core.Auditing.Models;
using Xunit;
using FluentAssertions;

namespace SqlMcp.Tests.Unit.Core.Auditing.Models
{
    /// <summary>
    /// Unit tests for security audit events
    /// </summary>
    public class SecurityAuditEventTests
    {
        [Fact]
        public void SecurityAuditEvent_Constructor_ShouldInitializeProperties()
        {
            // Arrange
            var eventType = SecurityEventType.LoginSuccess;
            var userId = "user123";
            var correlationId = "corr-456";
            var resource = "/api/users";

            // Act
            var auditEvent = new SecurityAuditEvent(
                eventType,
                userId,
                resource,
                correlationId);

            // Assert
            auditEvent.SecurityEventType.Should().Be(eventType);
            auditEvent.EventType.Should().Be("Security.LoginSuccess");
            auditEvent.Resource.Should().Be(resource);
            auditEvent.UserId.Should().Be(userId);
            auditEvent.CorrelationId.Should().Be(correlationId);
            auditEvent.Severity.Should().Be(AuditSeverity.Information);
        }

        [Theory]
        [InlineData(SecurityEventType.LoginSuccess, AuditSeverity.Information)]
        [InlineData(SecurityEventType.LoginFailure, AuditSeverity.Warning)]
        [InlineData(SecurityEventType.UnauthorizedAccess, AuditSeverity.Warning)]
        [InlineData(SecurityEventType.PermissionDenied, AuditSeverity.Warning)]
        [InlineData(SecurityEventType.TokenExpired, AuditSeverity.Information)]
        [InlineData(SecurityEventType.PasswordChanged, AuditSeverity.Information)]
        [InlineData(SecurityEventType.AccountLocked, AuditSeverity.Warning)]
        [InlineData(SecurityEventType.SuspiciousActivity, AuditSeverity.Critical)]
        public void SecurityAuditEvent_Severity_ShouldMatchEventType(
            SecurityEventType eventType,
            AuditSeverity expectedSeverity)
        {
            // Arrange & Act
            var auditEvent = new SecurityAuditEvent(eventType, "user", "/resource");

            // Assert
            auditEvent.Severity.Should().Be(expectedSeverity);
        }

        [Fact]
        public void SecurityAuditEvent_WithAuthenticationDetails_ShouldStoreCorrectly()
        {
            // Arrange
            var authMethod = "OAuth2";
            var authProvider = "AzureAD";

            // Act
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.LoginSuccess,
                "user123",
                "/api/login")
            {
                AuthenticationMethod = authMethod,
                AuthenticationProvider = authProvider
            };

            // Assert
            auditEvent.AuthenticationMethod.Should().Be(authMethod);
            auditEvent.AuthenticationProvider.Should().Be(authProvider);
        }

        [Fact]
        public void SecurityAuditEvent_WithPermissions_ShouldStoreCorrectly()
        {
            // Arrange
            var requiredPermissions = new List<string> { "Read", "Write" };
            var userPermissions = new List<string> { "Read" };

            // Act
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.PermissionDenied,
                "user123",
                "/api/users/update")
            {
                RequiredPermissions = requiredPermissions,
                UserPermissions = userPermissions
            };

            // Assert
            auditEvent.RequiredPermissions.Should().BeEquivalentTo(requiredPermissions);
            auditEvent.UserPermissions.Should().BeEquivalentTo(userPermissions);
            auditEvent.GetMissingPermissions().Should().BeEquivalentTo(new[] { "Write" });
        }

        [Fact]
        public void SecurityAuditEvent_WithReason_ShouldStoreCorrectly()
        {
            // Arrange
            var reason = "Invalid credentials";

            // Act
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.LoginFailure,
                "user123",
                "/api/login")
            {
                Reason = reason
            };

            // Assert
            auditEvent.Reason.Should().Be(reason);
        }

        [Fact]
        public void SecurityAuditEvent_WithRiskScore_ShouldValidateRange()
        {
            // Arrange
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.SuspiciousActivity,
                "user123",
                "/api/admin");

            // Act & Assert - Valid scores
            auditEvent.RiskScore = 0.0;
            auditEvent.RiskScore.Should().Be(0.0);

            auditEvent.RiskScore = 0.5;
            auditEvent.RiskScore.Should().Be(0.5);

            auditEvent.RiskScore = 1.0;
            auditEvent.RiskScore.Should().Be(1.0);

            // Act & Assert - Invalid scores should throw
            Action setNegative = () => auditEvent.RiskScore = -0.1;
            setNegative.Should().Throw<ArgumentOutOfRangeException>();

            Action setTooHigh = () => auditEvent.RiskScore = 1.1;
            setTooHigh.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SecurityAuditEvent_ToLogString_ShouldIncludeSecurityDetails()
        {
            // Arrange
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.UnauthorizedAccess,
                "user123",
                "/api/admin/users")
            {
                AuthenticationMethod = "JWT",
                Reason = "Insufficient privileges",
                RequiredPermissions = new List<string> { "Admin.Read", "Admin.Write" },
                UserPermissions = new List<string> { "User.Read" }
            };

            // Act
            var logString = auditEvent.ToLogString();

            // Assert
            logString.Should().Contain("Security.UnauthorizedAccess");
            logString.Should().Contain("Resource=/api/admin/users");
            logString.Should().Contain("AuthMethod=JWT");
            logString.Should().Contain("Reason=Insufficient privileges");
            logString.Should().Contain("MissingPermissions=Admin.Read,Admin.Write");
        }

        [Fact]
        public void SecurityAuditEvent_WithThreatIndicators_ShouldStoreCorrectly()
        {
            // Arrange
            var indicators = new Dictionary<string, object>
            {
                ["FailedAttempts"] = 5,
                ["LastFailedAttempt"] = DateTime.UtcNow,
                ["GeoLocation"] = "Unknown",
                ["UserAgent"] = "Suspicious Bot 1.0"
            };

            // Act
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.SuspiciousActivity,
                "user123",
                "/api/login")
            {
                ThreatIndicators = indicators,
                RiskScore = 0.85
            };

            // Assert
            auditEvent.ThreatIndicators.Should().BeEquivalentTo(indicators);
            auditEvent.RiskScore.Should().Be(0.85);
        }

        [Fact]
        public void SecurityAuditEvent_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new SecurityAuditEvent(
                SecurityEventType.LoginSuccess,
                "user123",
                "/api/login")
            {
                AuthenticationMethod = "OAuth2",
                RequiredPermissions = new List<string> { "Read", "Write" },
                ThreatIndicators = new Dictionary<string, object> { ["Key"] = "Value" }
            };

            // Act
            var clone = original.Clone() as SecurityAuditEvent;

            // Assert
            clone.Should().NotBeNull();
            clone.Should().NotBeSameAs(original);
            clone.SecurityEventType.Should().Be(original.SecurityEventType);
            clone.Resource.Should().Be(original.Resource);
            clone.RequiredPermissions.Should().NotBeSameAs(original.RequiredPermissions);
            clone.RequiredPermissions.Should().BeEquivalentTo(original.RequiredPermissions);
            clone.ThreatIndicators.Should().NotBeSameAs(original.ThreatIndicators);
        }

        [Fact]
        public void SecurityAuditEvent_GetMissingPermissions_WithNullLists_ShouldReturnEmpty()
        {
            // Arrange
            var auditEvent = new SecurityAuditEvent(
                SecurityEventType.PermissionDenied,
                "user123",
                "/api/resource");

            // Act
            var missing = auditEvent.GetMissingPermissions();

            // Assert
            missing.Should().BeEmpty();
        }
    }
}
