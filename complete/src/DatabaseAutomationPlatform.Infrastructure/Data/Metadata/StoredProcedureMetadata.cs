using System;
using System.Collections.Generic;
using System.Data;

namespace DatabaseAutomationPlatform.Infrastructure.Data.Metadata
{
    /// <summary>
    /// Metadata about a stored procedure for caching and validation
    /// </summary>
    public class StoredProcedureMetadata
    {
        /// <summary>
        /// The fully qualified name of the stored procedure (including schema)
        /// </summary>
        public string FullyQualifiedName { get; }

        /// <summary>
        /// The schema name
        /// </summary>
        public string SchemaName { get; }

        /// <summary>
        /// The procedure name
        /// </summary>
        public string ProcedureName { get; }

        /// <summary>
        /// Expected parameters for the stored procedure
        /// </summary>
        public IReadOnlyList<ParameterMetadata> Parameters { get; }

        /// <summary>
        /// Indicates if the procedure returns a result set
        /// </summary>
        public bool ReturnsResultSet { get; }

        /// <summary>
        /// Custom timeout for this specific procedure (in seconds)
        /// </summary>
        public int? TimeoutSeconds { get; }

        /// <summary>
        /// Security classification of the procedure
        /// </summary>
        public SecurityClassification SecurityLevel { get; }

        /// <summary>
        /// When this metadata was last validated
        /// </summary>
        public DateTime LastValidated { get; }

        /// <summary>
        /// Cache expiration time
        /// </summary>
        public DateTime CacheExpiration { get; }

        public StoredProcedureMetadata(
            string schemaName,
            string procedureName,
            IReadOnlyList<ParameterMetadata> parameters,
            bool returnsResultSet = false,
            int? timeoutSeconds = null,
            SecurityClassification securityLevel = SecurityClassification.Standard)
        {
            if (string.IsNullOrWhiteSpace(schemaName))
                throw new ArgumentException("Schema name cannot be null or empty", nameof(schemaName));
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentException("Procedure name cannot be null or empty", nameof(procedureName));

            SchemaName = schemaName;
            ProcedureName = procedureName;
            FullyQualifiedName = $"[{schemaName}].[{procedureName}]";
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            ReturnsResultSet = returnsResultSet;
            TimeoutSeconds = timeoutSeconds;
            SecurityLevel = securityLevel;
            LastValidated = DateTime.UtcNow;
            CacheExpiration = DateTime.UtcNow.AddHours(1); // Default 1-hour cache
        }

        /// <summary>
        /// Validates if the provided parameters match the expected metadata
        /// </summary>
        public ValidationResult ValidateParameters(IEnumerable<Parameters.StoredProcedureParameter> providedParameters)
        {
            var errors = new List<string>();
            var providedParamDict = new Dictionary<string, Parameters.StoredProcedureParameter>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var param in providedParameters)
            {
                providedParamDict[param.Name] = param;
            }

            // Check for required parameters
            foreach (var expectedParam in Parameters)
            {
                if (expectedParam.IsRequired && !providedParamDict.ContainsKey(expectedParam.Name))
                {
                    errors.Add($"Required parameter '{expectedParam.Name}' is missing");
                }
            }

            // Check for unexpected parameters (could indicate SQL injection attempt)
            foreach (var providedParam in providedParameters)
            {
                var matchFound = false;
                foreach (var expectedParam in Parameters)
                {
                    if (string.Equals(expectedParam.Name, providedParam.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        matchFound = true;
                        // Validate type compatibility
                        if (expectedParam.SqlDbType != providedParam.SqlDbType)
                        {
                            errors.Add($"Parameter '{providedParam.Name}' type mismatch. Expected: {expectedParam.SqlDbType}, Provided: {providedParam.SqlDbType}");
                        }
                        break;
                    }
                }

                if (!matchFound)
                {
                    errors.Add($"Unexpected parameter '{providedParam.Name}' provided");
                }
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// Checks if the metadata cache has expired
        /// </summary>
        public bool IsCacheExpired() => DateTime.UtcNow > CacheExpiration;
    }

    /// <summary>
    /// Metadata about a stored procedure parameter
    /// </summary>
    public class ParameterMetadata
    {
        public string Name { get; }
        public SqlDbType SqlDbType { get; }
        public bool IsRequired { get; }
        public ParameterDirection Direction { get; }
        public int? MaxLength { get; }
        public object? DefaultValue { get; }

        public ParameterMetadata(
            string name,
            SqlDbType sqlDbType,
            bool isRequired = true,
            ParameterDirection direction = ParameterDirection.Input,
            int? maxLength = null,
            object? defaultValue = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SqlDbType = sqlDbType;
            IsRequired = isRequired;
            Direction = direction;
            MaxLength = maxLength;
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// Security classification for stored procedures
    /// </summary>
    public enum SecurityClassification
    {
        /// <summary>
        /// Standard security - normal logging and access control
        /// </summary>
        Standard,

        /// <summary>
        /// Elevated security - additional audit logging required
        /// </summary>
        Elevated,

        /// <summary>
        /// Critical security - full audit trail and restricted access
        /// </summary>
        Critical
    }

    /// <summary>
    /// Result of parameter validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(bool isValid, IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
        }
    }
}