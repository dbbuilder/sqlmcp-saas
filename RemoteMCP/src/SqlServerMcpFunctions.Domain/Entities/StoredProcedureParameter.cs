using System;

namespace SqlServerMcpFunctions.Domain.Entities
{
    /// <summary>
    /// Represents a parameter for a stored procedure
    /// </summary>
    public class StoredProcedureParameter
    {
        /// <summary>
        /// Parameter name (without @ prefix)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// SQL Server data type
        /// </summary>
        public string SqlType { get; set; } = string.Empty;

        /// <summary>
        /// .NET CLR type
        /// </summary>
        public Type ClrType { get; set; } = typeof(object);

        /// <summary>
        /// Whether this is an input parameter
        /// </summary>
        public bool IsInput { get; set; } = true;

        /// <summary>
        /// Whether this is an output parameter
        /// </summary>
        public bool IsOutput { get; set; } = false;

        /// <summary>
        /// Whether this parameter is nullable
        /// </summary>
        public bool IsNullable { get; set; } = true;
        /// <summary>
        /// Maximum length for string/binary types
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Precision for decimal types
        /// </summary>
        public byte? Precision { get; set; }

        /// <summary>
        /// Scale for decimal types
        /// </summary>
        public byte? Scale { get; set; }

        /// <summary>
        /// Default value if any
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Human-readable description of the parameter
        /// </summary>
        public string? Description { get; set; }
    }
}
