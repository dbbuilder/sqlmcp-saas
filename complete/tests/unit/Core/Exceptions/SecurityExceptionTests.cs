using System;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for SecurityException class
    /// </summary>
    public class SecurityExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_ShouldUseGenericSafeMessage()
        {
            // Arrange
            const string detailedMessage = "User 'john@example.com' attempted to access resource 'admin/users' without permission";

            // Act
            var exception = new SecurityException(detailedMessage);

            // Assert
            exception.Message.Should().Be(detailedMessage);
            exception.SafeMessage.Should().Be("Access denied. You do not have permission to perform this action.");
            exception.SecurityEventType.Should().Be(SecurityEventType.Unauthorized);
            exception.UserId.Should().BeNull();
            exception.Resource.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithSecurityEventType_ShouldSetAppropriateMessage()
        {
            // Arrange & Act
            var authException = new SecurityException("Auth failed", SecurityEventType.AuthenticationFailure);
            var authzException = new SecurityException("Authz failed", SecurityEventType.AuthorizationFailure);
            var suspiciousException = new SecurityException("Suspicious", SecurityEventType.SuspiciousActivity);

            // Assert
            authException.SafeMessage.Should().Be("Authentication failed. Please check your credentials and try again.");
            authzException.SafeMessage.Should().Be("You are not authorized to access this resource.");
            suspiciousException.SafeMessage.Should().Be("Your request has been blocked for security reasons.");
        }

        [Fact]
        public void WithUserId_ShouldSetUserIdAndAddToDetails()
        {
            // Arrange
            var exception = new SecurityException("Unauthorized access");
            const string userId = "user123";

            // Act
            var result = exception.WithUserId(userId);

            // Assert
            result.Should().BeSameAs(exception);
            exception.UserId.Should().Be(userId);
            exception.Details.Should().ContainKey("UserId");
            exception.Details["UserId"].Should().Be(userId);
        }

        [Fact]
        public void WithResource_ShouldSetResourceAndAddToDetails()
        {
            // Arrange
            var exception = new SecurityException("Unauthorized access");
            const string resource = "/api/admin/users";

            // Act
            var result = exception.WithResource(resource);

            // Assert
            result.Should().BeSameAs(exception);
            exception.Resource.Should().Be(resource);
            exception.Details.Should().ContainKey("Resource");
            exception.Details["Resource"].Should().Be(resource);
        }

        [Fact]
        public void WithIpAddress_ShouldAddIpAddressToDetails()
        {
            // Arrange
            var exception = new SecurityException("Suspicious activity");
            const string ipAddress = "192.168.1.100";

            // Act
            var result = exception.WithIpAddress(ipAddress);

            // Assert
            result.Should().BeSameAs(exception);
            exception.Details.Should().ContainKey("IpAddress");
            exception.Details["IpAddress"].Should().Be(ipAddress);
        }

        [Fact]
        public void GetLogMessage_ShouldIncludeSecurityDetails()
        {
            // Arrange
            var exception = new SecurityException("Unauthorized access attempt", SecurityEventType.AuthorizationFailure)
                .WithUserId("user123")
                .WithResource("/api/admin/users")
                .WithIpAddress("192.168.1.100");

            // Act
            var logMessage = exception.GetLogMessage();

            // Assert
            logMessage.Should().Contain("SecurityEventType: AuthorizationFailure");
            logMessage.Should().Contain("UserId: user123");
            logMessage.Should().Contain("Resource: /api/admin/users");
            logMessage.Should().Contain("IpAddress: 192.168.1.100");
        }

        [Fact]
        public void FluentApi_ShouldAllowChaining()
        {
            // Arrange & Act
            var exception = new SecurityException("Security violation")
                .WithUserId("user123")
                .WithResource("/api/secure")
                .WithIpAddress("10.0.0.1");

            // Assert
            exception.UserId.Should().Be("user123");
            exception.Resource.Should().Be("/api/secure");
            exception.Details["IpAddress"].Should().Be("10.0.0.1");
        }

        [Fact]
        public void SafeMessage_ShouldNeverContainSensitiveInfo()
        {
            // Arrange
            var exception = new SecurityException(
                "User 'admin@company.com' from IP 192.168.1.100 failed authentication",
                SecurityEventType.AuthenticationFailure)
                .WithUserId("admin@company.com")
                .WithIpAddress("192.168.1.100");

            // Assert
            exception.SafeMessage.Should().NotContain("admin@company.com");
            exception.SafeMessage.Should().NotContain("192.168.1.100");
            exception.SafeMessage.Should().Be("Authentication failed. Please check your credentials and try again.");
        }
    }
}
