namespace DatabaseAutomationPlatform.Infrastructure.Security
{
    /// <summary>
    /// Provides secure connection strings from Azure Key Vault or configuration
    /// </summary>
    public interface ISecureConnectionStringProvider
    {
        /// <summary>
        /// Retrieves a connection string securely
        /// </summary>
        /// <param name="connectionName">Name of the connection string</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The connection string</returns>
        Task<string> GetConnectionStringAsync(
            string connectionName, 
            CancellationToken cancellationToken = default);
    }
}