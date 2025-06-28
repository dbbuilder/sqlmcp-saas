using System;
using System.Collections.Generic;

namespace SqlServerMcpFunctions.Domain.Entities
{
    /// <summary>
    /// Represents a database connection configuration
    /// </summary>
    public class DatabaseConnection
    {
        /// <summary>
        /// Unique identifier for this connection
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name for the connection
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Connection string (may be a Key Vault reference)
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Database provider type (SqlServer, PostgreSQL, etc.)
        /// </summary>
        public string ProviderType { get; set; } = "SqlServer";

        /// <summary>
        /// Whether this connection is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        /// <summary>
        /// Maximum number of concurrent connections
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        public int CommandTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Additional connection properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// When this connection was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this connection was last tested successfully
        /// </summary>
        public DateTime? LastTestedAt { get; set; }
    }
}
