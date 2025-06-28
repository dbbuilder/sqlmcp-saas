using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when validation fails.
    /// Contains field-level validation errors that are safe to display to users.
    /// </summary>
    public class ValidationException : BaseException
    {
        /// <summary>
        /// Gets the validation errors grouped by field name.
        /// </summary>
        public Dictionary<string, List<string>> ValidationErrors { get; }

        /// <summary>
        /// Gets whether there are any validation errors.
        /// </summary>
        public bool HasErrors => ValidationErrors.Any();

        /// <summary>
        /// Initializes a new instance of the ValidationException class with a message.
        /// </summary>
        /// <param name="message">The validation error message.</param>
        public ValidationException(string message) 
            : base(message, message) // Validation messages are safe to show to users
        {
            ValidationErrors = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Initializes a new instance of the ValidationException class with validation errors.
        /// </summary>
        /// <param name="validationErrors">The dictionary of validation errors.</param>
        public ValidationException(Dictionary<string, List<string>> validationErrors) 
            : base(
                BuildDetailedMessage(validationErrors), 
                BuildSafeMessage(validationErrors))
        {
            ValidationErrors = validationErrors ?? new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Adds a validation error for a specific field.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <param name="error">The error message.</param>
        public void AddValidationError(string field, string error)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (!ValidationErrors.ContainsKey(field))
            {
                ValidationErrors[field] = new List<string>();
            }

            ValidationErrors[field].Add(error);
        }

        /// <summary>
        /// Gets a formatted string representation of all validation errors.
        /// </summary>
        /// <returns>A formatted string of validation errors.</returns>
        public string GetFormattedErrors()
        {
            var sb = new StringBuilder();
            
            foreach (var fieldErrors in ValidationErrors)
            {
                sb.AppendLine($"{fieldErrors.Key}:");
                foreach (var error in fieldErrors.Value)
                {
                    sb.AppendLine($"  - {error}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds a detailed error message from validation errors.
        /// </summary>
        private static string BuildDetailedMessage(Dictionary<string, List<string>> validationErrors)
        {
            if (validationErrors == null || !validationErrors.Any())
            {
                return "Validation failed";
            }

            var errorCount = validationErrors.Sum(kvp => kvp.Value.Count);
            return $"Validation failed with {errorCount} error(s) in {validationErrors.Count} field(s)";
        }

        /// <summary>
        /// Builds a safe error message from validation errors.
        /// </summary>
        private static string BuildSafeMessage(Dictionary<string, List<string>> validationErrors)
        {
            if (validationErrors == null || !validationErrors.Any())
            {
                return "Validation failed";
            }

            return "Validation failed. Please check the provided values and try again.";
        }
    }
}
