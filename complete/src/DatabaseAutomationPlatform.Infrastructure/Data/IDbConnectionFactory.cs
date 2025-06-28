using System.Data;
using System.Data.Common;

namespace DatabaseAutomationPlatform.Infrastructure.Data
{
    /// <summary>
    /// Factory for creating secure database connections with retry policies
    /// </summary>
    public interface IDbConnectionFactory
    {
        /// <summary>
        /// Creates a new database connection with security and retry policies
        /// </summary>
        /// <param name="connectionName">Name of the connection string in configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Open database connection</returns>
        Task<IDbConnection> CreateConnectionAsync(
            string connectionName, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Creates a new database connection for a specific database
        /// </summary>
        /// <param name="connectionName">Name of the connection string in configuration</param>
        /// <param name="databaseName">Override database name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Open database connection</returns>
        Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            string databaseName,
            CancellationToken cancellationToken = default);
    }
}