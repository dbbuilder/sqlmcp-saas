using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for ValidationException class
    /// </summary>
    public class ValidationExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            const string message = "Validation failed";

            // Act
            var exception = new ValidationException(message);

            // Assert
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be(message); // Validation messages are safe to show
            exception.ValidationErrors.Should().NotBeNull();
            exception.ValidationErrors.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_WithValidationErrors_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var errors = new Dictionary<string, List<string>>
            {
                ["Email"] = new List<string> { "Email is required", "Email format is invalid" },
                ["Password"] = new List<string> { "Password must be at least 8 characters" }
            };

            // Act
            var exception = new ValidationException(errors);

            // Assert
            exception.Message.Should().Contain("Validation failed");
            exception.SafeMessage.Should().Contain("Validation failed");
            exception.ValidationErrors.Should().BeEquivalentTo(errors);
        }

        [Fact]
        public void AddValidationError_ShouldAddErrorToField()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");
            const string field = "Email";
            const string error = "Email is required";

            // Act
            exception.AddValidationError(field, error);

            // Assert
            exception.ValidationErrors.Should().ContainKey(field);
            exception.ValidationErrors[field].Should().Contain(error);
        }

        [Fact]
        public void AddValidationError_MultipleErrorsForSameField_ShouldAccumulate()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");
            const string field = "Email";
            const string error1 = "Email is required";
            const string error2 = "Email format is invalid";

            // Act
            exception.AddValidationError(field, error1);
            exception.AddValidationError(field, error2);

            // Assert
            exception.ValidationErrors[field].Should().HaveCount(2);
            exception.ValidationErrors[field].Should().Contain(error1);
            exception.ValidationErrors[field].Should().Contain(error2);
        }

        [Fact]
        public void AddValidationError_WithNullField_ShouldThrowArgumentNullException()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");

            // Act & Assert
            var act = () => exception.AddValidationError(null, "Error message");
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("field");
        }

        [Fact]
        public void AddValidationError_WithNullError_ShouldThrowArgumentNullException()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");

            // Act & Assert
            var act = () => exception.AddValidationError("Field", null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("error");
        }

        [Fact]
        public void GetFormattedErrors_ShouldReturnFormattedString()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");
            exception.AddValidationError("Email", "Email is required");
            exception.AddValidationError("Email", "Email format is invalid");
            exception.AddValidationError("Password", "Password must be at least 8 characters");

            // Act
            var formatted = exception.GetFormattedErrors();

            // Assert
            formatted.Should().Contain("Email:");
            formatted.Should().Contain("- Email is required");
            formatted.Should().Contain("- Email format is invalid");
            formatted.Should().Contain("Password:");
            formatted.Should().Contain("- Password must be at least 8 characters");
        }

        [Fact]
        public void HasErrors_WithNoErrors_ShouldReturnFalse()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");

            // Act & Assert
            exception.HasErrors.Should().BeFalse();
        }

        [Fact]
        public void HasErrors_WithErrors_ShouldReturnTrue()
        {
            // Arrange
            var exception = new ValidationException("Validation failed");
            exception.AddValidationError("Email", "Email is required");

            // Act & Assert
            exception.HasErrors.Should().BeTrue();
        }
    }
}
