namespace DatabaseAutomationPlatform.Infrastructure.Security
{
    /// <summary>
    /// Interface for managing secrets from Azure Key Vault
    /// </summary>
    public interface ISecretManager
    {
        /// <summary>
        /// Retrieves a secret value from Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <returns>Secret value</returns>
        Task<string> GetSecretAsync(string secretName);
        
        /// <summary>
        /// Sets or updates a secret value in Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <param name="secretValue">Value of the secret</param>
        /// <returns>Version of the created secret</returns>
        Task<string> SetSecretAsync(string secretName, string secretValue);
        
        /// <summary>
        /// Checks if a secret exists in Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <returns>True if secret exists</returns>
        Task<bool> SecretExistsAsync(string secretName);
        
        /// <summary>
        /// Deletes a secret from Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        Task DeleteSecretAsync(string secretName);
    }
}