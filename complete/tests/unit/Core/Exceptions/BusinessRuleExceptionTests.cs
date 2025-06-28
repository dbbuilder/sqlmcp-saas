using System;
using Xunit;
using FluentAssertions;
using SqlMcp.Core.Exceptions;

namespace SqlMcp.Tests.Unit.Core.Exceptions
{
    /// <summary>
    /// Unit tests for BusinessRuleException class
    /// </summary>
    public class BusinessRuleExceptionTests
    {
        [Fact]
        public void Constructor_WithRuleNameAndMessage_ShouldSetProperties()
        {
            // Arrange
            const string ruleName = "MinimumOrderAmount";
            const string message = "Order amount must be at least $10";

            // Act
            var exception = new BusinessRuleException(ruleName, message);

            // Assert
            exception.RuleName.Should().Be(ruleName);
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be(message); // Business rule messages are safe to show
            exception.Details.Should().ContainKey("RuleName");
            exception.Details["RuleName"].Should().Be(ruleName);
        }

        [Fact]
        public void Constructor_WithRuleNameMessageAndUserMessage_ShouldSetProperties()
        {
            // Arrange
            const string ruleName = "PasswordComplexity";
            const string message = "Password validation failed: missing uppercase letter";
            const string userMessage = "Password must contain at least one uppercase letter";

            // Act
            var exception = new BusinessRuleException(ruleName, message, userMessage);

            // Assert
            exception.RuleName.Should().Be(ruleName);
            exception.Message.Should().Be(message);
            exception.SafeMessage.Should().Be(userMessage);
        }

        [Fact]
        public void WithRuleCode_ShouldSetRuleCodeAndAddToDetails()
        {
            // Arrange
            var exception = new BusinessRuleException("DuplicateEmail", "Email already exists");
            const string ruleCode = "BR001";

            // Act
            var result = exception.WithRuleCode(ruleCode);

            // Assert
            result.Should().BeSameAs(exception);
            exception.RuleCode.Should().Be(ruleCode);
            exception.Details.Should().ContainKey("RuleCode");
            exception.Details["RuleCode"].Should().Be(ruleCode);
        }
    }
}
