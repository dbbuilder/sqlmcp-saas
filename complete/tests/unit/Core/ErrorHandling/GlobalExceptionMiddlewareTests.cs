using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.ErrorHandling;
using SqlMcp.Core.Exceptions;
using SqlMcp.Core.Models.Errors;

namespace SqlMcp.Tests.Unit.Core.ErrorHandling
{
    /// <summary>
    /// Unit tests for GlobalExceptionMiddleware
    /// </summary>
    public class GlobalExceptionMiddlewareTests
    {
        private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock;
        private readonly Mock<RequestDelegate> _nextMock;
        private readonly GlobalExceptionMiddleware _middleware;

        public GlobalExceptionMiddlewareTests()
        {
            _loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            _nextMock = new Mock<RequestDelegate>();
            _middleware = new GlobalExceptionMiddleware(_nextMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task InvokeAsync_WhenNoException_ShouldCallNext()
        {
            // Arrange
            var context = new DefaultHttpContext();
            _nextMock.Setup(next => next(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WhenValidationException_ShouldReturn400()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            var validationException = new ValidationException("Validation failed");
            validationException.AddValidationError("Email", "Email is required");

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(validationException);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
            context.Response.ContentType.Should().Be("application/json");

            // Verify response body
            responseStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseStream).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            errorResponse.Should().NotBeNull();
            errorResponse.CorrelationId.Should().Be(validationException.CorrelationId);
            errorResponse.Message.Should().Be(validationException.SafeMessage);
            errorResponse.Errors.Should().HaveCount(1);
        }

        [Fact]
        public async Task InvokeAsync_WhenResourceNotFoundException_ShouldReturn404()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            var notFoundException = new ResourceNotFoundException("User", "12345");

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(notFoundException);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task InvokeAsync_WhenSecurityException_ShouldReturn401Or403()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var authException = new SecurityException("Auth failed", SecurityEventType.AuthenticationFailure);
            var authzException = new SecurityException("Authz failed", SecurityEventType.AuthorizationFailure);

            _nextMock.SetupSequence(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(authException)
                .ThrowsAsync(authzException);

            // Act & Assert - Authentication failure
            await _middleware.InvokeAsync(context);
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);

            // Reset for next test
            context.Response.StatusCode = 200;

            // Act & Assert - Authorization failure
            await _middleware.InvokeAsync(context);
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_WhenDatabaseException_ShouldReturn503()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var dbException = new DatabaseException("Connection timeout", "GetUser");

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(dbException);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task InvokeAsync_WhenGeneralException_ShouldReturn500()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            var exception = new InvalidOperationException("Something went wrong");

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
            
            // Verify safe error message
            responseStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseStream).ReadToEndAsync();
            responseBody.Should().NotContain("Something went wrong");
            responseBody.Should().Contain("An error has occurred");
        }

        [Fact]
        public async Task InvokeAsync_WhenExceptionOccurs_ShouldLogError()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var exception = new InvalidOperationException("Test error");

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WhenOperationCanceledException_ShouldNotLog()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var exception = new OperationCanceledException();

            _nextMock.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(exception);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.RequestTimeout);
            _loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Never);
        }
    }
}
