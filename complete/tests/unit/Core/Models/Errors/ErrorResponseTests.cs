using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Models.Errors;

namespace SqlMcp.Tests.Unit.Core.Models.Errors
{
    /// <summary>
    /// Unit tests for ErrorResponse class
    /// </summary>
    public class ErrorResponseTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange
            const string correlationId = "test-correlation-id";
            const string message = "An error occurred";

            // Act
            var response = new ErrorResponse(correlationId, message);

            // Assert
            response.CorrelationId.Should().Be(correlationId);
            response.Message.Should().Be(message);
            response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            response.Errors.Should().NotBeNull();
            response.Errors.Should().BeEmpty();
        }

        [Fact]
        public void AddError_ShouldAddErrorDetail()
        {
            // Arrange
            var response = new ErrorResponse("test-id", "Error occurred");
            const string code = "VALIDATION_ERROR";
            const string message = "Field is required";
            const string field = "Email";

            // Act
            response.AddError(code, message, field);

            // Assert
            response.Errors.Should().HaveCount(1);
            var error = response.Errors[0];
            error.Code.Should().Be(code);
            error.Message.Should().Be(message);
            error.Field.Should().Be(field);
        }

        [Fact]
        public void AddError_WithoutField_ShouldAddErrorWithNullField()
        {
            // Arrange
            var response = new ErrorResponse("test-id", "Error occurred");
            const string code = "GENERAL_ERROR";
            const string message = "Something went wrong";

            // Act
            response.AddError(code, message);

            // Assert
            response.Errors.Should().HaveCount(1);
            var error = response.Errors[0];
            error.Code.Should().Be(code);
            error.Message.Should().Be(message);
            error.Field.Should().BeNull();
        }

        [Fact]
        public void FromException_WithBaseException_ShouldCreateProperResponse()
        {
            // Arrange
            var exception = new SqlMcp.Core.Exceptions.ValidationException("Validation failed");
            exception.AddValidationError("Email", "Email is required");
            exception.AddValidationError("Password", "Password is too short");

            // Act
            var response = ErrorResponse.FromException(exception);

            // Assert
            response.CorrelationId.Should().Be(exception.CorrelationId);
            response.Message.Should().Be(exception.SafeMessage);
            response.Timestamp.Should().Be(exception.Timestamp);
            response.Errors.Should().HaveCount(2);
        }

        [Fact]
        public void FromException_WithRegularException_ShouldCreateGenericResponse()
        {
            // Arrange
            var exception = new InvalidOperationException("Something bad happened");

            // Act
            var response = ErrorResponse.FromException(exception);

            // Assert
            response.CorrelationId.Should().NotBeNullOrWhiteSpace();
            response.Message.Should().Be("An error has occurred. Please try again later.");
            response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }
}
