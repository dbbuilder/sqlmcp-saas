using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;
using DatabaseAutomationPlatform.Infrastructure.Logging.Configuration;

namespace DatabaseAutomationPlatform.Tests.Unit.Infrastructure.Logging
{
    public class CorrelationIdEnricherTests
    {
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<ILogEventPropertyFactory> _propertyFactoryMock;
        private readonly CorrelationIdEnricher _sut;

        public CorrelationIdEnricherTests()
        {
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _propertyFactoryMock = new Mock<ILogEventPropertyFactory>();
            _sut = new CorrelationIdEnricher(_httpContextAccessorMock.Object);
        }

        [Fact]
        public void Constructor_WithNullHttpContextAccessor_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CorrelationIdEnricher(null!);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("httpContextAccessor");
        }

        [Fact]
        public void Enrich_WithNullLogEvent_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => _sut.Enrich(null!, _propertyFactoryMock.Object);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logEvent");
        }

        [Fact]
        public void Enrich_WithNullPropertyFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var logEvent = new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Information,
                null,
                MessageTemplate.Empty,
                new List<LogEventProperty>());

            // Act & Assert
            var act = () => _sut.Enrich(logEvent, null!);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("propertyFactory");
        }

        [Fact]
        public void Enrich_WithCorrelationIdInItems_UsesItemsCorrelationId()
        {
            // Arrange
            var expectedCorrelationId = "test-correlation-id";
            var context = new DefaultHttpContext();
            context.Items["CorrelationId"] = expectedCorrelationId;
            
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
            
            var property = new LogEventProperty("CorrelationId", new ScalarValue(expectedCorrelationId));
            _propertyFactoryMock
                .Setup(x => x.CreateProperty("CorrelationId", expectedCorrelationId))
                .Returns(property);

            var logEvent = CreateLogEvent();

            // Act
            _sut.Enrich(logEvent, _propertyFactoryMock.Object);

            // Assert
            _propertyFactoryMock.Verify(
                x => x.CreateProperty("CorrelationId", expectedCorrelationId),
                Times.Once);
        }

        [Fact]
        public void Enrich_WithCorrelationIdInHeader_UsesHeaderCorrelationId()
        {
            // Arrange
            var expectedCorrelationId = "header-correlation-id";
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-ID"] = expectedCorrelationId;
            
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
            
            var property = new LogEventProperty("CorrelationId", new ScalarValue(expectedCorrelationId));
            _propertyFactoryMock
                .Setup(x => x.CreateProperty("CorrelationId", expectedCorrelationId))
                .Returns(property);

            var logEvent = CreateLogEvent();

            // Act
            _sut.Enrich(logEvent, _propertyFactoryMock.Object);

            // Assert
            _propertyFactoryMock.Verify(
                x => x.CreateProperty("CorrelationId", expectedCorrelationId),
                Times.Once);
        }

        [Fact]
        public void Enrich_WithTraceIdentifier_UsesTraceIdentifier()
        {
            // Arrange
            var expectedCorrelationId = "trace-identifier";
            var context = new DefaultHttpContext();
            context.TraceIdentifier = expectedCorrelationId;
            
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
            
            var property = new LogEventProperty("CorrelationId", new ScalarValue(expectedCorrelationId));
            _propertyFactoryMock
                .Setup(x => x.CreateProperty("CorrelationId", expectedCorrelationId))
                .Returns(property);

            var logEvent = CreateLogEvent();

            // Act
            _sut.Enrich(logEvent, _propertyFactoryMock.Object);

            // Assert
            _propertyFactoryMock.Verify(
                x => x.CreateProperty("CorrelationId", expectedCorrelationId),
                Times.Once);
        }

        [Fact]
        public void Enrich_WithNoHttpContext_GeneratesNewGuid()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            
            var property = new LogEventProperty("CorrelationId", new ScalarValue("any-guid"));
            _propertyFactoryMock
                .Setup(x => x.CreateProperty(
                    "CorrelationId", 
                    It.Is<string>(s => Guid.TryParse(s, out _))))
                .Returns(property);

            var logEvent = CreateLogEvent();

            // Act
            _sut.Enrich(logEvent, _propertyFactoryMock.Object);

            // Assert
            _propertyFactoryMock.Verify(
                x => x.CreateProperty(
                    "CorrelationId", 
                    It.Is<string>(s => Guid.TryParse(s, out _))),
                Times.Once);
        }

        [Fact]
        public void Enrich_PrefersItemsOverHeader()
        {
            // Arrange
            var itemsCorrelationId = "items-correlation-id";
            var headerCorrelationId = "header-correlation-id";
            
            var context = new DefaultHttpContext();
            context.Items["CorrelationId"] = itemsCorrelationId;
            context.Request.Headers["X-Correlation-ID"] = headerCorrelationId;
            
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
            
            var property = new LogEventProperty("CorrelationId", new ScalarValue(itemsCorrelationId));
            _propertyFactoryMock
                .Setup(x => x.CreateProperty("CorrelationId", itemsCorrelationId))
                .Returns(property);

            var logEvent = CreateLogEvent();

            // Act
            _sut.Enrich(logEvent, _propertyFactoryMock.Object);

            // Assert
            _propertyFactoryMock.Verify(
                x => x.CreateProperty("CorrelationId", itemsCorrelationId),
                Times.Once);
            _propertyFactoryMock.Verify(
                x => x.CreateProperty("CorrelationId", headerCorrelationId),
                Times.Never);
        }

        private static LogEvent CreateLogEvent()
        {
            return new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Information,
                null,
                MessageTemplate.Empty,
                new List<LogEventProperty>());
        }
    }
}