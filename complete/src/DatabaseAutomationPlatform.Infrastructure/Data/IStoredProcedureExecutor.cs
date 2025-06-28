using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAutomationPlatform.Infrastructure.Data.Parameters;
using DatabaseAutomationPlatform.Infrastructure.Data.Results;

namespace DatabaseAutomationPlatform.Infrastructure.Data
{
    /// <summary>
    /// Interface for executing stored procedures with comprehensive security and audit features
    /// </summary>
    public interface IStoredProcedureExecutor
    {
        /// <summary>
        /// Executes a stored procedure that doesn't return a result set (INSERT, UPDATE, DELETE)
        /// </summary>
        /// <param name="procedureName">Name of the stored procedure (can include schema)</param>
        /// <param name="parameters">Collection of strongly-typed parameters</param>
        /// <param name="timeoutSeconds">Optional timeout override</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with affected rows count and execution metadata</returns>
        Task<StoredProcedureResult<int>> ExecuteNonQueryAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure that returns a single scalar value
        /// </summary>
        /// <typeparam name="T">Type of the scalar result</typeparam>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Collection of strongly-typed parameters</param>
        /// <param name="timeoutSeconds">Optional timeout override</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with scalar value and execution metadata</returns>
        Task<StoredProcedureResult<T>> ExecuteScalarAsync<T>(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure that returns a result set
        /// </summary>
        /// <typeparam name="T">Type to map each row to</typeparam>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="mapper">Function to map data reader to result type</param>
        /// <param name="parameters">Collection of strongly-typed parameters</param>
        /// <param name="timeoutSeconds">Optional timeout override</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with enumerable data and execution metadata</returns>
        Task<StoredProcedureResult<IEnumerable<T>>> ExecuteReaderAsync<T>(
            string procedureName,
            Func<IDataReader, T> mapper,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure that returns multiple result sets
        /// </summary>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Collection of strongly-typed parameters</param>
        /// <param name="timeoutSeconds">Optional timeout override</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with DataSet containing all result sets</returns>
        Task<StoredProcedureResult<DataSet>> ExecuteDataSetAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure in a transaction
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="executeAction">Action to execute within the transaction</param>
        /// <param name="isolationLevel">Transaction isolation level</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the transaction execution</returns>
        Task<StoredProcedureResult<T>> ExecuteInTransactionAsync<T>(
            string procedureName,
            Func<IDbTransaction, Task<T>> executeAction,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a stored procedure exists and parameters match expected schema
        /// </summary>
        /// <param name="procedureName">Name of the stored procedure</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with any errors or warnings</returns>
        Task<bool> ValidateProcedureAsync(
            string procedureName,
            IEnumerable<StoredProcedureParameter>? parameters = null,
            CancellationToken cancellationToken = default);
    }
}