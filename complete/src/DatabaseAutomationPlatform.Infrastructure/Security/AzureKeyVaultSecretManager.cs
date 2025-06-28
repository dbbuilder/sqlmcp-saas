using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;

namespace DatabaseAutomationPlatform.Infrastructure.Security
{
    /// <summary>
    /// Azure Key Vault secret manager implementation with caching and retry policies
    /// </summary>
    public class AzureKeyVaultSecretManager : ISecretManager
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<AzureKeyVaultSecretManager> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ConcurrentDictionary<string, CachedSecret> _cache;
        private readonly TimeSpan _cacheExpiration;
        private const int MaxRetryCount = 3;
        private const int RetryDelayMilliseconds = 500;

        private class CachedSecret
        {
            public string Value { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public bool IsValid => DateTime.UtcNow < ExpiresAt;
        }

        public AzureKeyVaultSecretManager(            IConfiguration configuration,
            ILogger<AzureKeyVaultSecretManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var keyVaultUri = configuration["Azure:KeyVault:VaultUri"] 
                ?? throw new InvalidOperationException("Azure Key Vault URI not configured");
            
            // Use DefaultAzureCredential which works with Managed Identity in production
            // and Azure CLI/Visual Studio authentication in development
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeInteractiveBrowserCredential = true
            });

            _secretClient = new SecretClient(new Uri(keyVaultUri), credential);
            _cache = new ConcurrentDictionary<string, CachedSecret>();
            _cacheExpiration = TimeSpan.FromMinutes(
                configuration.GetValue<int>("Azure:KeyVault:CacheExpirationMinutes", 5));

            // Configure retry policy for transient failures
            _retryPolicy = Policy
                .Handle<RequestFailedException>(IsTransientError)
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(                    MaxRetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(RetryDelayMilliseconds * retryAttempt),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Key Vault operation retry {RetryCount} after {TimeSpan}ms due to: {ExceptionMessage}",
                            retryCount, timeSpan.TotalMilliseconds, exception.Message);
                    });

            _logger.LogInformation("Azure Key Vault Secret Manager initialized for vault: {VaultUri}", 
                keyVaultUri);
        }

        /// <inheritdoc/>
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (string.IsNullOrWhiteSpace(secretName))
            {
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));
            }

            try
            {
                // Check cache first
                if (_cache.TryGetValue(secretName, out var cachedSecret) && cachedSecret.IsValid)
                {
                    _logger.LogDebug("Retrieved secret {SecretName} from cache", secretName);
                    return cachedSecret.Value;
                }

                _logger.LogDebug("Retrieving secret {SecretName} from Key Vault", secretName);
                // Retrieve from Key Vault with retry policy
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _secretClient.GetSecretAsync(secretName));

                if (response?.Value == null)
                {
                    throw new InvalidOperationException($"Secret '{secretName}' not found in Key Vault");
                }

                // Update cache
                _cache.AddOrUpdate(secretName, 
                    new CachedSecret 
                    { 
                        Value = response.Value.Value, 
                        ExpiresAt = DateTime.UtcNow.Add(_cacheExpiration) 
                    },
                    (key, existing) => new CachedSecret 
                    { 
                        Value = response.Value.Value, 
                        ExpiresAt = DateTime.UtcNow.Add(_cacheExpiration) 
                    });

                _logger.LogInformation("Successfully retrieved secret {SecretName} from Key Vault", 
                    secretName);
                
                return response.Value.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Secret {SecretName} not found in Key Vault", secretName);
                throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault", ex);
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> SetSecretAsync(string secretName, string secretValue)
        {
            if (string.IsNullOrWhiteSpace(secretName))
            {
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));
            }

            if (secretValue == null)
            {
                throw new ArgumentNullException(nameof(secretValue));
            }

            try
            {
                _logger.LogDebug("Setting secret {SecretName} in Key Vault", secretName);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _secretClient.SetSecretAsync(secretName, secretValue));

                // Invalidate cache
                _cache.TryRemove(secretName, out _);

                _logger.LogInformation("Successfully set secret {SecretName} in Key Vault", secretName);
                
                return response.Value.Properties.Version;
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set secret {SecretName} in Key Vault", secretName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SecretExistsAsync(string secretName)
        {
            try
            {
                await GetSecretAsync(secretName);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteSecretAsync(string secretName)
        {
            if (string.IsNullOrWhiteSpace(secretName))
            {
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));
            }

            try
            {
                _logger.LogDebug("Deleting secret {SecretName} from Key Vault", secretName);

                await _retryPolicy.ExecuteAsync(async () =>
                    await _secretClient.StartDeleteSecretAsync(secretName));
                // Remove from cache
                _cache.TryRemove(secretName, out _);

                _logger.LogInformation("Successfully initiated deletion of secret {SecretName} from Key Vault", 
                    secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete secret {SecretName} from Key Vault", secretName);
                throw;
            }
        }

        /// <summary>
        /// Determines if an Azure request exception is transient
        /// </summary>
        private static bool IsTransientError(RequestFailedException ex)
        {
            // HTTP status codes that indicate transient errors
            return ex.Status == 429 || // Too Many Requests
                   ex.Status == 500 || // Internal Server Error
                   ex.Status == 502 || // Bad Gateway
                   ex.Status == 503 || // Service Unavailable
                   ex.Status == 504;   // Gateway Timeout
        }
    }
}