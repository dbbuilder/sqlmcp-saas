using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when database operations fail.
    /// Sanitizes error messages to prevent information disclosure.
    /// </summary>
    public class DatabaseException : BaseException
    {
        private const string SafeDatabaseMessage = "A database error occurred. Please try again or contact support.";
        
        // Common transient SQL Server error numbers
        private static readonly HashSet<int> TransientErrorNumbers = new HashSet<int>
        {
            1205,  // Deadlock
            -2,    // Timeout
            49918, // Cannot process request. Not enough resources to process request.
            49919, // Cannot process create or update request. Too many create or update operations in progress
            49920, // Cannot process request. Too many operations in progress
            41839, // Transaction exceeded the maximum number of commit dependencies
            41325, // The current transaction failed to commit due to a serializable validation failure
            41305, // The current transaction failed to commit due to a repeatable read validation failure
            41302, // The current transaction attempted to update a record that has been updated since the transaction started
            41301  // Dependency failure
        };

        /// <summary>
        /// Gets the database operation that failed.
        /// </summary>
        public string DatabaseOperation { get; private set; }

        /// <summary>
        /// Gets the SQL error number if available.
        /// </summary>
        public int? SqlErrorNumber { get; private set; }

        /// <summary>
        /// Gets whether this is a transient error that could be retried.
        /// </summary>
        public bool IsTransient => SqlErrorNumber.HasValue && TransientErrorNumbers.Contains(SqlErrorNumber.Value);

        /// <summary>
        /// Initializes a new instance of the DatabaseException class.
        /// </summary>
        /// <param name="message">The detailed error message (will be sanitized for users).</param>
        public DatabaseException(string message) 
            : base(message, SafeDatabaseMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseException class with operation details.
        /// </summary>
        /// <param name="message">The detailed error message.</param>
        /// <param name="operation">The database operation that failed.</param>
        public DatabaseException(string message, string operation) 
            : base(message, SafeDatabaseMessage)
        {
            DatabaseOperation = operation;
            if (!string.IsNullOrWhiteSpace(operation))
            {
                AddDetail("Operation", operation);
            }
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseException class with an inner exception.
        /// </summary>
        /// <param name="message">The detailed error message.</param>
        /// <param name="operation">The database operation that failed.</param>
        /// <param name="innerException">The inner exception.</param>
        public DatabaseException(string message, string operation, Exception innerException) 
            : base(message, SafeDatabaseMessage, innerException)
        {
            DatabaseOperation = operation;
            if (!string.IsNullOrWhiteSpace(operation))
            {
                AddDetail("Operation", operation);
            }

            // Extract SQL error number if inner exception is SqlException
            if (innerException is SqlException sqlEx)
            {
                WithSqlErrorNumber(sqlEx.Number);
            }
        }

        /// <summary>
        /// Sets the SQL error number for this exception.
        /// </summary>
        /// <param name="errorNumber">The SQL error number.</param>
        /// <returns>This exception instance for fluent chaining.</returns>
        public DatabaseException WithSqlErrorNumber(int errorNumber)
        {
            SqlErrorNumber = errorNumber;
            AddDetail("SqlErrorNumber", errorNumber.ToString());
            return this;
        }
    }
}
