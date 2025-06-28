using System;
using System.Data.SqlClient;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for DatabaseException class
    /// </summary>
    public class DatabaseExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_ShouldSetSafeMessage()
        {
            // Arrange
            const string detailedMessage = "Cannot insert duplicate key in object 'dbo.Users'. The duplicate key value is (john@example.com).";

            // Act
            var exception = new DatabaseException(detailedMessage);

            // Assert
            exception.Message.Should().Be(detailedMessage);
            exception.SafeMessage.Should().Be("A database error occurred. Please try again or contact support.");
            exception.DatabaseOperation.Should().BeNull();
            exception.SqlErrorNumber.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithMessageAndOperation_ShouldSetProperties()
        {
            // Arrange
            const string message = "Database connection timeout";
            const string operation = "GetUserById";

            // Act
            var exception = new DatabaseException(message, operation);

            // Assert
            exception.Message.Should().Be(message);
            exception.DatabaseOperation.Should().Be(operation);
            exception.Details.Should().ContainKey("Operation");
            exception.Details["Operation"].Should().Be(operation);
        }

        [Fact]
        public void Constructor_WithSqlException_ShouldExtractErrorDetails()
        {
            // Arrange
            // Note: We can't create a real SqlException, so we'll test with a mock inner exception
            var innerException = new InvalidOperationException("Connection timeout");
            const string operation = "ExecuteQuery";

            // Act
            var exception = new DatabaseException("Database error", operation, innerException);

            // Assert
            exception.InnerException.Should().Be(innerException);
            exception.DatabaseOperation.Should().Be(operation);
        }

        [Fact]
        public void WithSqlErrorNumber_ShouldSetErrorNumber()
        {
            // Arrange
            var exception = new DatabaseException("Database error");
            const int errorNumber = 2627; // Violation of unique constraint

            // Act
            var result = exception.WithSqlErrorNumber(errorNumber);

            // Assert
            result.Should().BeSameAs(exception); // Fluent API
            exception.SqlErrorNumber.Should().Be(errorNumber);
            exception.Details.Should().ContainKey("SqlErrorNumber");
            exception.Details["SqlErrorNumber"].Should().Be(errorNumber.ToString());
        }

        [Fact]
        public void SanitizeMessage_ShouldRemoveSensitiveInformation()
        {
            // Arrange
            const string messageWithTableName = "Invalid object name 'dbo.Users'";
            const string messageWithColumnName = "Invalid column name 'SSN'";
            const string messageWithQuery = "Error executing: SELECT * FROM Users WHERE Email='test@example.com'";

            // Act
            var exception1 = new DatabaseException(messageWithTableName);
            var exception2 = new DatabaseException(messageWithColumnName);
            var exception3 = new DatabaseException(messageWithQuery);

            // Assert
            exception1.SafeMessage.Should().NotContain("dbo.Users");
            exception2.SafeMessage.Should().NotContain("SSN");
            exception3.SafeMessage.Should().NotContain("SELECT");
            exception3.SafeMessage.Should().NotContain("test@example.com");
        }

        [Fact]
        public void IsTransient_WithTransientError_ShouldReturnTrue()
        {
            // Arrange
            var exception = new DatabaseException("Database error")
                .WithSqlErrorNumber(1205); // Deadlock

            // Act & Assert
            exception.IsTransient.Should().BeTrue();
        }

        [Theory]
        [InlineData(1205)] // Deadlock
        [InlineData(-2)]   // Timeout
        [InlineData(2601)] // Cannot insert duplicate key row
        [InlineData(2627)] // Violation of unique constraint
        public void IsTransient_ShouldIdentifyTransientErrors(int errorNumber)
        {
            // Arrange
            var exception = new DatabaseException("Database error")
                .WithSqlErrorNumber(errorNumber);

            // Act
            var isTransient = exception.IsTransient;

            // Assert
            if (errorNumber == 1205 || errorNumber == -2)
            {
                isTransient.Should().BeTrue();
            }
            else
            {
                isTransient.Should().BeFalse();
            }
        }

        [Fact]
        public void GetLogMessage_ShouldIncludeDatabaseSpecificDetails()
        {
            // Arrange
            var exception = new DatabaseException("Database error", "GetUserById")
                .WithSqlErrorNumber(2627);

            // Act
            var logMessage = exception.GetLogMessage();

            // Assert
            logMessage.Should().Contain("Operation: GetUserById");
            logMessage.Should().Contain("SqlErrorNumber: 2627");
        }
    }
}
