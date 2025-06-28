using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DatabaseAutomationPlatform.Infrastructure.Data.Security
{
    /// <summary>
    /// Provides parameter sanitization and validation to prevent SQL injection
    /// </summary>
    public class ParameterSanitizer
    {
        private readonly ILogger<ParameterSanitizer> _logger;
        
        // Patterns that might indicate SQL injection attempts
        private static readonly string[] SuspiciousPatterns = new[]
        {
            @"(^|[^a-zA-Z])union(\s+all)?\s+select",
            @"(^|[^a-zA-Z])drop\s+(table|database|schema|procedure|function)",
            @"(^|[^a-zA-Z])exec(ute)?\s+",
            @"(^|[^a-zA-Z])insert\s+into",
            @"(^|[^a-zA-Z])update\s+.+\s+set",
            @"(^|[^a-zA-Z])delete\s+from",
            @"(^|[^a-zA-Z])create\s+(table|database|schema|procedure|function)",
            @"(^|[^a-zA-Z])alter\s+(table|database|schema|procedure|function)",
            @"(^|[^a-zA-Z])truncate\s+table",
            @"xp_cmdshell",
            @"sp_executesql",
            @"(^|[^a-zA-Z])waitfor\s+delay",
            @"(;|--|\*/|/\*)",  // Comment indicators
            @"@@version",
            @"@@servername",
            @"information_schema",
            @"sysobjects",
            @"syscolumns",
            @"char\s*\(\s*\d+\s*\+\s*\d+\s*\)",  // char(65+66) style attacks
            @"(0x[0-9a-f]+)",  // Hex encoded values
            @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]"  // Control characters
        };

        private static readonly Regex[] SuspiciousRegexes = SuspiciousPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();

        // Maximum reasonable lengths for common parameter types
        private static readonly Dictionary<Type, int> MaxLengthByType = new Dictionary<Type, int>
        {
            { typeof(string), 8000 },  // SQL Server nvarchar(max) practical limit
            { typeof(byte[]), 2147483647 },  // SQL Server varbinary(max)
        };

        public ParameterSanitizer(ILogger<ParameterSanitizer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates and sanitizes a collection of parameters
        /// </summary>
        public SanitizationResult ValidateParameters(
            string procedureName,
            IEnumerable<Parameters.StoredProcedureParameter> parameters)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var sanitizedParameters = new List<Parameters.StoredProcedureParameter>();

            foreach (var parameter in parameters)
            {
                var paramResult = ValidateParameter(procedureName, parameter);
                
                if (!paramResult.IsValid)
                {
                    errors.AddRange(paramResult.Errors);
                }
                
                warnings.AddRange(paramResult.Warnings);
                sanitizedParameters.Add(parameter);
            }

            var result = new SanitizationResult(
                isValid: errors.Count == 0,
                errors: errors,
                warnings: warnings,
                sanitizedParameters: sanitizedParameters);

            // Log security events
            if (!result.IsValid || result.Warnings.Any())
            {
                _logger.LogWarning(
                    "Parameter validation issues for procedure {ProcedureName}. Errors: {ErrorCount}, Warnings: {WarningCount}",
                    procedureName, errors.Count, warnings.Count);
                
                foreach (var error in errors)
                {
                    _logger.LogError("Parameter validation error: {Error}", error);
                }
            }

            return result;
        }

        /// <summary>
        /// Validates a single parameter
        /// </summary>
        private ParameterValidationResult ValidateParameter(
            string procedureName,
            Parameters.StoredProcedureParameter parameter)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate parameter name
            if (!IsValidParameterName(parameter.Name))
            {
                errors.Add($"Invalid parameter name: {parameter.Name}");
            }

            // Validate parameter value
            if (parameter.Value != null && parameter.Value != DBNull.Value)
            {
                var valueType = parameter.Value.GetType();
                
                // Check string values for SQL injection patterns
                if (valueType == typeof(string))
                {
                    var stringValue = (string)parameter.Value;
                    
                    // Check length
                    if (stringValue.Length > MaxLengthByType[typeof(string)])
                    {
                        errors.Add($"Parameter {parameter.Name} exceeds maximum length");
                    }

                    // Check for suspicious patterns
                    foreach (var regex in SuspiciousRegexes)
                    {
                        if (regex.IsMatch(stringValue))
                        {
                            warnings.Add($"Parameter {parameter.Name} contains suspicious pattern: {regex.ToString()}");
                            
                            // Log potential SQL injection attempt
                            _logger.LogWarning(
                                "Potential SQL injection attempt detected. Procedure: {Procedure}, Parameter: {Parameter}, Pattern: {Pattern}",
                                procedureName, parameter.Name, regex.ToString());
                        }
                    }

                    // Check for null bytes
                    if (stringValue.Contains('\0'))
                    {
                        errors.Add($"Parameter {parameter.Name} contains null bytes");
                    }
                }
                
                // Validate byte arrays
                else if (valueType == typeof(byte[]))
                {
                    var byteArray = (byte[])parameter.Value;
                    if (byteArray.Length > MaxLengthByType[typeof(byte[])])
                    {
                        errors.Add($"Parameter {parameter.Name} byte array exceeds maximum length");
                    }
                }
            }

            return new ParameterValidationResult(
                isValid: errors.Count == 0,
                errors: errors,
                warnings: warnings);
        }

        /// <summary>
        /// Validates parameter name format
        /// </summary>
        private bool IsValidParameterName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Parameter name should start with @ and contain only valid characters
            var validNamePattern = @"^@[a-zA-Z_][a-zA-Z0-9_]*$";
            return Regex.IsMatch(name, validNamePattern);
        }

        /// <summary>
        /// Escapes special characters in string values (if needed for display/logging)
        /// </summary>
        public static string EscapeForLogging(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Replace control characters with their escape sequences
            return Regex.Replace(value, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", 
                m => $"\\x{((int)m.Value[0]):X2}");
        }
    }

    /// <summary>
    /// Result of parameter sanitization
    /// </summary>
    public class SanitizationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<Parameters.StoredProcedureParameter> SanitizedParameters { get; }

        public SanitizationResult(
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            IReadOnlyList<Parameters.StoredProcedureParameter> sanitizedParameters)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
            SanitizedParameters = sanitizedParameters ?? new List<Parameters.StoredProcedureParameter>();
        }
    }

    /// <summary>
    /// Result of single parameter validation
    /// </summary>
    internal class ParameterValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        public ParameterValidationResult(
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }
    }
}