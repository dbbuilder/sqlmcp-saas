using System;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for ConfigurationException class
    /// </summary>
    public class ConfigurationExceptionTests
    {
        [Fact]
        public void Constructor_WithConfigKey_ShouldSetProperties()
        {
            // Arrange
            const string configKey = "DatabaseConnectionString";

            // Act
            var exception = new ConfigurationException(configKey);

            // Assert
            exception.ConfigurationKey.Should().Be(configKey);
            exception.Message.Should().Contain(configKey);
            exception.SafeMessage.Should().Be("The application is not configured correctly. Please contact support.");
        }

        [Fact]
        public void Constructor_WithConfigKeyAndMessage_ShouldSetProperties()
        {
            // Arrange
            const string configKey = "ApiKey";
            const string message = "API key is missing or invalid";

            // Act
            var exception = new ConfigurationException(configKey, message);

            // Assert
            exception.ConfigurationKey.Should().Be(configKey);
            exception.Message.Should().Be(message);
            exception.Details.Should().ContainKey("ConfigurationKey");
            exception.Details["ConfigurationKey"].Should().Be(configKey);
        }

        [Fact]
        public void Constructor_WithConfigKeyMessageAndInnerException_ShouldSetProperties()
        {
            // Arrange
            const string configKey = "RedisConnection";
            const string message = "Failed to parse Redis connection string";
            var innerException = new FormatException("Invalid format");

            // Act
            var exception = new ConfigurationException(configKey, message, innerException);

            // Assert
            exception.ConfigurationKey.Should().Be(configKey);
            exception.Message.Should().Be(message);
            exception.InnerException.Should().Be(innerException);
        }
    }
}
