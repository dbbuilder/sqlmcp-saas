using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DatabaseAutomationPlatform.Infrastructure.Data.Results
{
    /// <summary>
    /// Represents the result of a stored procedure execution with metadata
    /// </summary>
    /// <typeparam name="T">The type of data returned by the stored procedure</typeparam>
    public class StoredProcedureResult<T>
    {
        /// <summary>
        /// The data returned by the stored procedure
        /// </summary>
        public T? Data { get; }

        /// <summary>
        /// Indicates if the execution was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// The stored procedure that was executed
        /// </summary>
        public string ProcedureName { get; }

        /// <summary>
        /// Execution duration in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; }

        /// <summary>
        /// Number of rows affected (for non-query operations)
        /// </summary>
        public int RowsAffected { get; }

        /// <summary>
        /// Output parameter values after execution
        /// </summary>
        public Dictionary<string, object?> OutputParameters { get; }

        /// <summary>
        /// Return value from the stored procedure
        /// </summary>
        public int? ReturnValue { get; }

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryCount { get; }

        /// <summary>
        /// Unique execution ID for correlation
        /// </summary>
        public string ExecutionId { get; }

        /// <summary>
        /// Timestamp when execution started
        /// </summary>
        public DateTime ExecutionStartTime { get; }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static StoredProcedureResult<T> Success(
            T? data,
            string procedureName,
            long executionTimeMs,
            int rowsAffected = 0,
            Dictionary<string, object?>? outputParameters = null,
            int? returnValue = null,
            int retryCount = 0)
        {
            return new StoredProcedureResult<T>(
                data: data,
                isSuccess: true,
                errorMessage: null,
                procedureName: procedureName,
                executionTimeMs: executionTimeMs,
                rowsAffected: rowsAffected,
                outputParameters: outputParameters,
                returnValue: returnValue,
                retryCount: retryCount);
        }

        /// <summary>
        /// Creates a failure result
        /// </summary>
        public static StoredProcedureResult<T> Failure(
            string errorMessage,
            string procedureName,
            long executionTimeMs,
            int retryCount = 0)
        {
            return new StoredProcedureResult<T>(
                data: default,
                isSuccess: false,
                errorMessage: errorMessage,
                procedureName: procedureName,
                executionTimeMs: executionTimeMs,
                rowsAffected: 0,
                outputParameters: null,
                returnValue: null,
                retryCount: retryCount);
        }

        private StoredProcedureResult(
            T? data,
            bool isSuccess,
            string? errorMessage,
            string procedureName,
            long executionTimeMs,
            int rowsAffected,
            Dictionary<string, object?>? outputParameters,
            int? returnValue,
            int retryCount)
        {
            Data = data;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ProcedureName = procedureName ?? throw new ArgumentNullException(nameof(procedureName));
            ExecutionTimeMs = executionTimeMs;
            RowsAffected = rowsAffected;
            OutputParameters = outputParameters ?? new Dictionary<string, object?>();
            ReturnValue = returnValue;
            RetryCount = retryCount;
            ExecutionId = Guid.NewGuid().ToString();
            ExecutionStartTime = DateTime.UtcNow.AddMilliseconds(-executionTimeMs);
        }

        /// <summary>
        /// Gets execution metadata for logging
        /// </summary>
        public Dictionary<string, object?> GetExecutionMetadata()
        {
            return new Dictionary<string, object?>
            {
                ["ExecutionId"] = ExecutionId,
                ["ProcedureName"] = ProcedureName,
                ["IsSuccess"] = IsSuccess,
                ["ExecutionTimeMs"] = ExecutionTimeMs,
                ["RowsAffected"] = RowsAffected,
                ["RetryCount"] = RetryCount,
                ["ReturnValue"] = ReturnValue,
                ["OutputParameterCount"] = OutputParameters.Count,
                ["ExecutionStartTime"] = ExecutionStartTime
            };
        }
    }
}