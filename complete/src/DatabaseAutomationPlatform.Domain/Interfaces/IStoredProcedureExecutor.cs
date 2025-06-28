using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Interfaces
{
    /// <summary>
    /// Interface for executing stored procedures with full parameterization
    /// </summary>
    public interface IStoredProcedureExecutor
    {
        /// <summary>
        /// Execute a stored procedure and return multiple result sets
        /// </summary>
        /// <param name="connectionIdentifier">Database connection identifier</param>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Input parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result with data and output parameters</returns>
        Task<StoredProcedureResult> ExecuteAsync(
            string connectionIdentifier,
            string procedureName,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a stored procedure and return a single scalar value
        /// </summary>
        /// <typeparam name="T">Type of the scalar result</typeparam>
        /// <param name="connectionIdentifier">Database connection identifier</param>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Input parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scalar result value</returns>
        Task<T?> ExecuteScalarAsync<T>(
            string connectionIdentifier,
            string procedureName,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a stored procedure without returning data (INSERT, UPDATE, DELETE)
        /// </summary>
        /// <param name="connectionIdentifier">Database connection identifier</param>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Input parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of rows affected</returns>
        Task<int> ExecuteNonQueryAsync(
            string connectionIdentifier,
            string procedureName,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of stored procedure execution
    /// </summary>
    public class StoredProcedureResult
    {
        /// <summary>
        /// Result sets returned by the procedure
        /// </summary>
        public List<DataTable> ResultSets { get; set; } = new();

        /// <summary>
        /// Output parameter values
        /// </summary>
        public Dictionary<string, object?> OutputParameters { get; set; } = new();

        /// <summary>
        /// Return value from the procedure
        /// </summary>
        public int? ReturnValue { get; set; }

        /// <summary>
        /// Number of rows affected (for non-query operations)
        /// </summary>
        public int RowsAffected { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Whether the execution was successful
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
