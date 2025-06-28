using System;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for BaseException class
    /// </summary>
    public class BaseExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_ShouldSetProperties()
        {
            // Arrange
            const string message = "Test error message";
            const string safeMessage = "An error occurred";

            // Act
            var exception = new BaseException(message, safeMessage);

            // Assert
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be(safeMessage);
            exception.CorrelationId.Should().NotBeNullOrWhiteSpace();
            exception.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            exception.Details.Should().NotBeNull();
            exception.Details.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetProperties()
        {
            // Arrange
            const string message = "Test error message";
            const string safeMessage = "An error occurred";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new BaseException(message, safeMessage, innerException);

            // Assert
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be(safeMessage);
            exception.InnerException.Should().Be(innerException);
            exception.CorrelationId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void CorrelationId_ShouldBeUnique()
        {
            // Arrange & Act
            var exception1 = new BaseException("Error 1", "Safe message 1");
            var exception2 = new BaseException("Error 2", "Safe message 2");

            // Assert
            exception1.CorrelationId.Should().NotBe(exception2.CorrelationId);
        }

        [Fact]
        public void CorrelationId_ShouldBeValidGuid()
        {
            // Arrange & Act
            var exception = new BaseException("Error", "Safe message");

            // Assert
            Guid.TryParse(exception.CorrelationId, out var correlationId).Should().BeTrue();
            correlationId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public void AddDetail_ShouldAddToDetailsCollection()
        {
            // Arrange
            var exception = new BaseException("Error", "Safe message");
            const string key = "UserId";
            const string value = "12345";

            // Act
            exception.AddDetail(key, value);

            // Assert
            exception.Details.Should().ContainKey(key);
            exception.Details[key].Should().Be(value);
        }

        [Fact]
        public void AddDetail_WithNullKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var exception = new BaseException("Error", "Safe message");

            // Act & Assert
            var act = () => exception.AddDetail(null, "value");
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("key");
        }

        [Fact]
        public void AddDetail_WithExistingKey_ShouldUpdateValue()
        {
            // Arrange
            var exception = new BaseException("Error", "Safe message");
            const string key = "UserId";
            const string originalValue = "12345";
            const string newValue = "67890";

            // Act
            exception.AddDetail(key, originalValue);
            exception.AddDetail(key, newValue);

            // Assert
            exception.Details[key].Should().Be(newValue);
        }

        [Fact]
        public void WithCorrelationId_ShouldSetSpecificCorrelationId()
        {
            // Arrange
            var specificId = Guid.NewGuid().ToString();
            var exception = new BaseException("Error", "Safe message");

            // Act
            var result = exception.WithCorrelationId(specificId);

            // Assert
            result.Should().BeSameAs(exception); // Fluent API
            exception.CorrelationId.Should().Be(specificId);
        }

        [Fact]
        public void GetLogMessage_ShouldIncludeAllRelevantInformation()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");
            var exception = new BaseException("Detailed error message", "Safe message", innerException);
            exception.AddDetail("UserId", "12345");
            exception.AddDetail("Operation", "UpdateUser");

            // Act
            var logMessage = exception.GetLogMessage();

            // Assert
            logMessage.Should().Contain(exception.CorrelationId);
            logMessage.Should().Contain("Detailed error message");
            logMessage.Should().Contain("UserId: 12345");
            logMessage.Should().Contain("Operation: UpdateUser");
            logMessage.Should().Contain("Inner error");
        }

        [Fact]
        public void SafeMessage_WithNullValue_ShouldUseDefaultMessage()
        {
            // Arrange & Act
            var exception = new BaseException("Detailed error", null);

            // Assert
            exception.SafeMessage.Should().Be("An error has occurred. Please try again later.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SafeMessage_WithEmptyOrWhitespace_ShouldUseDefaultMessage(string safeMessage)
        {
            // Arrange & Act
            var exception = new BaseException("Detailed error", safeMessage);

            // Assert
            exception.SafeMessage.Should().Be("An error has occurred. Please try again later.");
        }
    }
}
