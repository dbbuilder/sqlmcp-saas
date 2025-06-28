using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using DatabaseAutomationPlatform.Infrastructure.Logging.Configuration;

namespace DatabaseAutomationPlatform.Tests.Unit.Infrastructure.Logging
{
    public class LoggingOptionsTests
    {
        [Fact]
        public void Constructor_ShouldSetDefaultValues()
        {
            // Act
            var options = new LoggingOptions();

            // Assert
            options.ApplicationInsightsInstrumentationKey.Should().BeEmpty();
            options.ApplicationInsightsConnectionString.Should().BeNull();
            options.MinimumLevel.Should().Be("Information");
            options.MicrosoftMinimumLevel.Should().Be("Warning");
            options.SystemMinimumLevel.Should().Be("Warning");
            options.EnableConsoleLogging.Should().BeTrue();
            options.EnableFileLogging.Should().BeFalse();
            options.FileLoggingPath.Should().Be("logs/log-.txt");
            options.RollingInterval.Should().Be("Day");
            options.RetainedFileCountLimit.Should().Be(31);
            options.EnableStructuredLogging.Should().BeTrue();
            options.EnableSensitiveDataLogging.Should().BeFalse();
            options.EnableRequestResponseLogging.Should().BeTrue();
            options.MaxRequestBodySize.Should().Be(32768);
            options.EnablePerformanceLogging.Should().BeTrue();
            options.PerformanceLoggingThreshold.Should().Be(1000);
            options.EnableSecurityEventLogging.Should().BeTrue();
            options.EnableCorrelationId.Should().BeTrue();
            options.EnableMachineName.Should().BeTrue();
            options.EnableEnvironmentName.Should().BeTrue();
            options.CustomProperties.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Validate_WithValidConfiguration_ShouldNotThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "test-key"
            };

            // Act & Assert
            options.Invoking(o => o.Validate()).Should().NotThrow();
        }

        [Fact]
        public void Validate_WithConnectionString_ShouldNotThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsConnectionString = "InstrumentationKey=test-key;IngestionEndpoint=https://test.com"
            };

            // Act & Assert
            options.Invoking(o => o.Validate()).Should().NotThrow();
        }

        [Fact]
        public void Validate_WithoutInstrumentationKeyOrConnectionString_ShouldThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "",
                ApplicationInsightsConnectionString = null
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                .Should().Throw<ValidationException>()
                .WithMessage("*ApplicationInsightsInstrumentationKey or ApplicationInsightsConnectionString must be provided*");
        }

        [Fact]
        public void Validate_WithFileLoggingEnabledButNoPath_ShouldThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "test-key",
                EnableFileLogging = true,
                FileLoggingPath = ""
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                .Should().Throw<ValidationException>()
                .WithMessage("*FileLoggingPath must be provided when EnableFileLogging is true*");
        }

        [Fact]
        public void Validate_WithNegativeMaxRequestBodySize_ShouldThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "test-key",
                MaxRequestBodySize = -1
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                .Should().Throw<ValidationException>()
                .WithMessage("*MaxRequestBodySize must be non-negative*");
        }

        [Fact]
        public void Validate_WithNegativePerformanceLoggingThreshold_ShouldThrow()
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "test-key",
                PerformanceLoggingThreshold = -1
            };

            // Act & Assert
            options.Invoking(o => o.Validate())
                .Should().Throw<ValidationException>()
                .WithMessage("*PerformanceLoggingThreshold must be non-negative*");
        }

        [Theory]
        [InlineData("Verbose")]
        [InlineData("Debug")]
        [InlineData("Information")]
        [InlineData("Warning")]
        [InlineData("Error")]
        [InlineData("Fatal")]
        public void MinimumLevel_ShouldAcceptValidLogLevels(string level)
        {
            // Arrange
            var options = new LoggingOptions
            {
                ApplicationInsightsInstrumentationKey = "test-key",
                MinimumLevel = level
            };

            // Act & Assert
            options.Invoking(o => o.Validate()).Should().NotThrow();
        }

        [Fact]
        public void CustomProperties_ShouldBeModifiable()
        {
            // Arrange
            var options = new LoggingOptions();

            // Act
            options.CustomProperties.Add("Environment", "Production");
            options.CustomProperties.Add("Application", "DatabaseAutomation");

            // Assert
            options.CustomProperties.Should().HaveCount(2);
            options.CustomProperties["Environment"].Should().Be("Production");
            options.CustomProperties["Application"].Should().Be("DatabaseAutomation");
        }
    }
}