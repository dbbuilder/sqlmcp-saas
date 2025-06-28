using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Security;

namespace DatabaseAutomationPlatform.Infrastructure.Security
{
    /// <summary>
    /// Secure connection string provider with Key Vault integration and caching
    /// </summary>
    public class SecureConnectionStringProvider : ISecureConnectionStringProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecureConnectionStringProvider> _logger;
        private readonly IMemoryCache _cache;
        private readonly SecretClient _secretClient;
        private readonly AsyncRetryPolicy _retryPolicy;
        private const string CacheKeyPrefix = "ConnectionString:";
        private const int CacheExpirationMinutes = 60;

        public SecureConnectionStringProvider(
            IConfiguration configuration,
            ILogger<SecureConnectionStringProvider> logger,
            IMemoryCache cache)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            // Initialize Azure Key Vault client
            var keyVaultUrl = _configuration["AzureKeyVault:VaultUrl"];
            if (!string.IsNullOrWhiteSpace(keyVaultUrl))
            {
                _secretClient = new SecretClient(
                    new Uri(keyVaultUrl), 
                    new DefaultAzureCredential());
                    
                _logger.LogInformation("Initialized Key Vault client for {VaultUrl}", keyVaultUrl);
            }
            else
            {
                _logger.LogWarning("Azure Key Vault URL not configured. Using local configuration only.");
            }

            // Configure retry policy for transient failures
            _retryPolicy = Policy
                .Handle<Azure.RequestFailedException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Retry {RetryCount} after {TimeSpan}s for Key Vault operation",
                            retryCount, 
                            timeSpan.TotalSeconds);
                    });
        }
        public async Task<string> GetConnectionStringAsync(
            string connectionName, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

            var cacheKey = $"{CacheKeyPrefix}{connectionName}";
            
            // Try to get from cache first
            if (_cache.TryGetValue<string>(cacheKey, out var cachedConnectionString))
            {
                _logger.LogDebug("Retrieved connection string from cache for {ConnectionName}", connectionName);
                return cachedConnectionString;
            }

            try
            {
                _logger.LogInformation("Retrieving connection string for {ConnectionName}", connectionName);
                
                // Try configuration first (for local development)
                var connectionString = _configuration.GetConnectionString(connectionName);
                
                if (string.IsNullOrWhiteSpace(connectionString) && _secretClient != null)
                {
                    // Retrieve from Key Vault
                    var secretName = $"ConnectionString-{connectionName}";
                    _logger.LogDebug("Retrieving secret {SecretName} from Key Vault", secretName);
                    
                    var response = await _retryPolicy.ExecuteAsync(async () =>
                        await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken));
                    
                    connectionString = response.Value.Value;
                    _logger.LogInformation("Successfully retrieved connection string from Key Vault for {ConnectionName}", connectionName);
                }

                // Validate connection string
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogError("Connection string '{ConnectionName}' not found in configuration or Key Vault", connectionName);
                    throw new SecurityException($"Connection string '{connectionName}' not found");
                }

                // Cache the connection string
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes),
                    Priority = CacheItemPriority.High
                };
                
                _cache.Set(cacheKey, connectionString, cacheOptions);
                
                _logger.LogInformation("Successfully retrieved and cached connection string for {ConnectionName}", connectionName);
                return connectionString;
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                _logger.LogError(ex, "Failed to retrieve connection string for {ConnectionName}", connectionName);
                throw new SecurityException($"Failed to retrieve secure connection string: {connectionName}", ex);
            }
        }
    }
}