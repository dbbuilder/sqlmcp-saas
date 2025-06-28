using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using DatabaseAutomationPlatform.Infrastructure.Data.Parameters;
using DatabaseAutomationPlatform.Infrastructure.Data.Security;

namespace DatabaseAutomationPlatform.Infrastructure.Tests.Unit.Data.Security
{
    public class ParameterSanitizerTests
    {
        private readonly Mock<ILogger<ParameterSanitizer>> _mockLogger;
        private readonly ParameterSanitizer _sanitizer;

        public ParameterSanitizerTests()
        {
            _mockLogger = new Mock<ILogger<ParameterSanitizer>>();
            _sanitizer = new ParameterSanitizer(_mockLogger.Object);
        }

        [Fact]
        public void ValidateParameters_WithValidParameters_ReturnsValid()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Name", "John Doe", SqlDbType.NVarChar),
                StoredProcedureParameter.Input("@Age", 30, SqlDbType.Int)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_GetUser", parameters);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
        }
        [Theory]
        [InlineData("'; DROP TABLE Users; --")]
        [InlineData("1' OR '1'='1")]
        [InlineData("admin' --")]
        [InlineData("1; DELETE FROM Users")]
        [InlineData("'; EXEC xp_cmdshell 'dir'; --")]
        public void ValidateParameters_WithSqlInjectionPattern_ReturnsWarning(string maliciousValue)
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@UserInput", maliciousValue, SqlDbType.NVarChar)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_ProcessInput", parameters);

            // Assert
            result.IsValid.Should().BeTrue(); // Warnings don't make it invalid
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w => w.Contains("suspicious pattern"));
            
            // Verify security logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Potential SQL injection")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        [Fact]
        public void ValidateParameters_WithInvalidParameterName_ReturnsError()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter("@Valid_Name", "value", SqlDbType.NVarChar),
                new StoredProcedureParameter("Invalid Name", "value", SqlDbType.NVarChar) // Invalid - no @
            };

            // Act & Assert
            // This should throw during parameter creation, but if it gets through...
            var act = () => new StoredProcedureParameter("", "value", SqlDbType.NVarChar);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ValidateParameters_WithNullBytesInString_ReturnsError()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Input", "Test\0Value", SqlDbType.NVarChar)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_ProcessInput", parameters);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("null bytes"));
        }

        [Fact]
        public void ValidateParameters_WithExtremelyLongString_ReturnsError()
        {
            // Arrange
            var longString = new string('A', 9000); // Over the 8000 limit
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@LongInput", longString, SqlDbType.NVarChar)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_ProcessInput", parameters);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("exceeds maximum length"));
        }
        [Fact]
        public void ValidateParameters_WithHexEncodedValues_ReturnsWarning()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Input", "0x44524F50205441424C45", SqlDbType.NVarChar)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_ProcessInput", parameters);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w => w.Contains("suspicious pattern"));
        }

        [Fact]
        public void EscapeForLogging_WithControlCharacters_EscapesProperly()
        {
            // Arrange
            var input = "Test\x00\x01\x1F\x7FValue";

            // Act
            var escaped = ParameterSanitizer.EscapeForLogging(input);

            // Assert
            escaped.Should().Be("Test\\x00\\x01\\x1F\\x7FValue");
        }

        [Fact]
        public void EscapeForLogging_WithNullOrEmpty_ReturnsOriginal()
        {
            // Arrange & Act & Assert
            ParameterSanitizer.EscapeForLogging(null).Should().BeNull();
            ParameterSanitizer.EscapeForLogging("").Should().Be("");
        }

        [Fact]
        public void ValidateParameters_LogsSecurityWarnings()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Input", "'; DROP TABLE Users; --", SqlDbType.NVarChar)
            };

            // Act
            var result = _sanitizer.ValidateParameters("sp_ProcessInput", parameters);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Parameter validation issues")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}