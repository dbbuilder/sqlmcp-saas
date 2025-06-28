using System;
using System.Collections.Generic;
using SqlMcp.Core.Auditing.Models;
using Xunit;
using FluentAssertions;

namespace SqlMcp.Tests.Unit.Core.Auditing.Models
{
    /// <summary>
    /// Unit tests for the base audit event interface and implementation
    /// </summary>
    public class AuditEventTests
    {
        [Fact]
        public void AuditEvent_Constructor_ShouldInitializeRequiredProperties()
        {
            // Arrange
            var eventType = "TestEvent";
            var userId = "user123";
            var correlationId = Guid.NewGuid().ToString();

            // Act
            var auditEvent = new AuditEvent(eventType, userId, correlationId);

            // Assert
            auditEvent.EventId.Should().NotBeEmpty();
            auditEvent.EventType.Should().Be(eventType);
            auditEvent.UserId.Should().Be(userId);
            auditEvent.CorrelationId.Should().Be(correlationId);
            auditEvent.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
            auditEvent.MachineName.Should().NotBeNullOrEmpty();
            auditEvent.ApplicationName.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void AuditEvent_WithNullUserId_ShouldDefaultToSystem()
        {
            // Arrange & Act
            var auditEvent = new AuditEvent("TestEvent", null, Guid.NewGuid().ToString());

            // Assert
            auditEvent.UserId.Should().Be("SYSTEM");
        }

        [Fact]
        public void AuditEvent_WithNullCorrelationId_ShouldGenerateNew()
        {
            // Arrange & Act
            var auditEvent = new AuditEvent("TestEvent", "user123", null);

            // Assert
            auditEvent.CorrelationId.Should().NotBeNullOrEmpty();
            Guid.TryParse(auditEvent.CorrelationId, out _).Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void AuditEvent_WithInvalidEventType_ShouldThrowArgumentException(string eventType)
        {
            // Arrange & Act
            var action = () => new AuditEvent(eventType, "user123", null);

            // Assert
            action.Should().Throw<ArgumentException>()
                .WithMessage("*eventType*");
        }

        [Fact]
        public void AuditEvent_AdditionalData_ShouldBeSettable()
        {
            // Arrange
            var auditEvent = new AuditEvent("TestEvent", "user123", null);
            var additionalData = new Dictionary<string, object>
            {
                ["Key1"] = "Value1",
                ["Key2"] = 123,
                ["Key3"] = true
            };

            // Act
            auditEvent.AdditionalData = additionalData;

            // Assert
            auditEvent.AdditionalData.Should().BeEquivalentTo(additionalData);
        }

        [Fact]
        public void AuditEvent_Severity_ShouldDefaultToInformation()
        {
            // Arrange & Act
            var auditEvent = new AuditEvent("TestEvent", "user123", null);

            // Assert
            auditEvent.Severity.Should().Be(AuditSeverity.Information);
        }

        [Fact]
        public void AuditEvent_WithIpAddress_ShouldStoreCorrectly()
        {
            // Arrange
            var ipAddress = "192.168.1.100";
            var auditEvent = new AuditEvent("TestEvent", "user123", null);

            // Act
            auditEvent.IpAddress = ipAddress;

            // Assert
            auditEvent.IpAddress.Should().Be(ipAddress);
        }

        [Fact]
        public void AuditEvent_WithSessionId_ShouldStoreCorrectly()
        {
            // Arrange
            var sessionId = "session-12345";
            var auditEvent = new AuditEvent("TestEvent", "user123", null);

            // Act
            auditEvent.SessionId = sessionId;

            // Assert
            auditEvent.SessionId.Should().Be(sessionId);
        }

        [Fact]
        public void AuditEvent_ToLogString_ShouldReturnFormattedString()
        {
            // Arrange
            var auditEvent = new AuditEvent("UserLogin", "user123", "corr-123")
            {
                IpAddress = "192.168.1.100",
                SessionId = "session-123"
            };

            // Act
            var logString = auditEvent.ToLogString();

            // Assert
            logString.Should().Contain("UserLogin");
            logString.Should().Contain("user123");
            logString.Should().Contain("corr-123");
            logString.Should().Contain("192.168.1.100");
        }

        [Fact]
        public void AuditEvent_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new AuditEvent("TestEvent", "user123", "corr-123")
            {
                IpAddress = "192.168.1.100",
                SessionId = "session-123",
                AdditionalData = new Dictionary<string, object> { ["Key"] = "Value" }
            };

            // Act
            var clone = original.Clone();

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.EventId.Should().NotBe(original.EventId); // New event ID
            clone.EventType.Should().Be(original.EventType);
            clone.UserId.Should().Be(original.UserId);
            clone.CorrelationId.Should().Be(original.CorrelationId);
            clone.AdditionalData.Should().NotBeSameAs(original.AdditionalData);
            clone.AdditionalData.Should().BeEquivalentTo(original.AdditionalData);
        }
    }
}
