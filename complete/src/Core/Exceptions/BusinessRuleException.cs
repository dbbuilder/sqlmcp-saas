using System;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when business rules are violated.
    /// These messages are typically safe to show to users.
    /// </summary>
    public class BusinessRuleException : BaseException
    {
        /// <summary>
        /// Gets the name of the business rule that was violated.
        /// </summary>
        public string RuleName { get; }

        /// <summary>
        /// Gets the optional business rule code.
        /// </summary>
        public string RuleCode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the BusinessRuleException class.
        /// </summary>
        /// <param name="ruleName">The name of the violated business rule.</param>
        /// <param name="message">The error message (safe to show to users).</param>
        public BusinessRuleException(string ruleName, string message) 
            : base(message, message) // Business rule messages are typically safe to show
        {
            RuleName = ruleName;
            AddDetail("RuleName", ruleName);
        }

        /// <summary>
        /// Initializes a new instance of the BusinessRuleException class with separate messages.
        /// </summary>
        /// <param name="ruleName">The name of the violated business rule.</param>
        /// <param name="message">The detailed error message for logging.</param>
        /// <param name="userMessage">The message safe to show to users.</param>
        public BusinessRuleException(string ruleName, string message, string userMessage) 
            : base(message, userMessage)
        {
            RuleName = ruleName;
            AddDetail("RuleName", ruleName);
        }

        /// <summary>
        /// Sets the business rule code.
        /// </summary>
        /// <param name="ruleCode">The rule code.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public BusinessRuleException WithRuleCode(string ruleCode)
        {
            RuleCode = ruleCode;
            if (!string.IsNullOrWhiteSpace(ruleCode))
            {
                AddDetail("RuleCode", ruleCode);
            }
            return this;
        }
    }
}
