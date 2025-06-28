using System;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for ResourceNotFoundException class
    /// </summary>
    public class ResourceNotFoundExceptionTests
    {
        [Fact]
        public void Constructor_WithResourceTypeAndId_ShouldSetProperties()
        {
            // Arrange
            const string resourceType = "User";
            const string resourceId = "12345";

            // Act
            var exception = new ResourceNotFoundException(resourceType, resourceId);

            // Assert
            exception.ResourceType.Should().Be(resourceType);
            exception.ResourceId.Should().Be(resourceId);
            exception.Message.Should().Contain(resourceType);
            exception.Message.Should().Contain(resourceId);
            exception.SafeMessage.Should().Be($"{resourceType} not found");
        }

        [Fact]
        public void Constructor_WithResourceTypeIdAndMessage_ShouldSetProperties()
        {
            // Arrange
            const string resourceType = "Product";
            const string resourceId = "SKU-123";
            const string message = "Product with SKU-123 not found in inventory";

            // Act
            var exception = new ResourceNotFoundException(resourceType, resourceId, message);

            // Assert
            exception.ResourceType.Should().Be(resourceType);
            exception.ResourceId.Should().Be(resourceId);
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be($"{resourceType} not found");
        }

        [Fact]
        public void Details_ShouldContainResourceTypeAndId()
        {
            // Arrange & Act
            var exception = new ResourceNotFoundException("Order", "ORD-789");

            // Assert
            exception.Details.Should().ContainKey("ResourceType");
            exception.Details["ResourceType"].Should().Be("Order");
            exception.Details.Should().ContainKey("ResourceId");
            exception.Details["ResourceId"].Should().Be("ORD-789");
        }
    }
}
