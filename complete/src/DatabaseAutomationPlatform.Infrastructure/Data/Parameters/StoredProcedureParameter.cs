using System;
using System.Data;
using System.Data.Common;

namespace DatabaseAutomationPlatform.Infrastructure.Data.Parameters
{
    /// <summary>
    /// Represents a parameter for a stored procedure with strong typing and validation
    /// </summary>
    public class StoredProcedureParameter
    {
        /// <summary>
        /// The name of the parameter (including @ prefix for SQL Server)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The value of the parameter
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// The SQL data type of the parameter
        /// </summary>
        public SqlDbType SqlDbType { get; }

        /// <summary>
        /// The direction of the parameter (Input, Output, InputOutput, ReturnValue)
        /// </summary>
        public ParameterDirection Direction { get; }

        /// <summary>
        /// The size of the parameter (for string and binary types)
        /// </summary>
        public int? Size { get; }

        /// <summary>
        /// The precision for numeric types
        /// </summary>
        public byte? Precision { get; }

        /// <summary>
        /// The scale for numeric types
        /// </summary>
        public byte? Scale { get; }

        /// <summary>
        /// Indicates if this parameter contains sensitive data that should not be logged
        /// </summary>
        public bool IsSensitive { get; }

        /// <summary>
        /// Creates a new stored procedure parameter
        /// </summary>
        public StoredProcedureParameter(
            string name,
            object? value,
            SqlDbType sqlDbType,
            ParameterDirection direction = ParameterDirection.Input,
            int? size = null,
            byte? precision = null,
            byte? scale = null,
            bool isSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter name cannot be null or empty", nameof(name));

            // Ensure parameter name starts with @
            Name = name.StartsWith("@") ? name : $"@{name}";
            Value = value;
            SqlDbType = sqlDbType;
            Direction = direction;
            Size = size;
            Precision = precision;
            Scale = scale;
            IsSensitive = isSensitive;
        }

        /// <summary>
        /// Creates an input parameter
        /// </summary>
        public static StoredProcedureParameter Input(string name, object? value, SqlDbType sqlDbType, bool isSensitive = false)
        {
            return new StoredProcedureParameter(name, value, sqlDbType, ParameterDirection.Input, isSensitive: isSensitive);
        }

        /// <summary>
        /// Creates an output parameter
        /// </summary>
        public static StoredProcedureParameter Output(string name, SqlDbType sqlDbType, int? size = null)
        {
            return new StoredProcedureParameter(name, null, sqlDbType, ParameterDirection.Output, size);
        }

        /// <summary>
        /// Creates a return value parameter
        /// </summary>
        public static StoredProcedureParameter ReturnValue()
        {
            return new StoredProcedureParameter("@ReturnValue", null, SqlDbType.Int, ParameterDirection.ReturnValue);
        }

        /// <summary>
        /// Applies this parameter to a database command
        /// </summary>
        public void ApplyToCommand(DbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var parameter = command.CreateParameter();
            parameter.ParameterName = Name;
            parameter.Value = Value ?? DBNull.Value;
            parameter.Direction = Direction;

            // Set SQL Server specific properties if available
            if (parameter is System.Data.SqlClient.SqlParameter sqlParameter)
            {
                sqlParameter.SqlDbType = SqlDbType;
                
                if (Size.HasValue)
                    sqlParameter.Size = Size.Value;
                
                if (Precision.HasValue)
                    sqlParameter.Precision = Precision.Value;
                
                if (Scale.HasValue)
                    sqlParameter.Scale = Scale.Value;
            }

            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Gets a safe string representation for logging (masks sensitive values)
        /// </summary>
        public string ToLogString()
        {
            var displayValue = IsSensitive ? "***REDACTED***" : (Value?.ToString() ?? "NULL");
            return $"{Name}={displayValue} (Type: {SqlDbType}, Direction: {Direction})";
        }
    }
}