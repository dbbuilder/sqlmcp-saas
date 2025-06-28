# Database Connection Factory - Implementation Guide

## Overview

This guide provides step-by-step instructions for implementing a secure database connection factory for the Database Automation Platform. The implementation follows security best practices and includes comprehensive error handling and logging.

## Prerequisites

- .NET 8.0 SDK installed
- Visual Studio 2022 or VS Code
- Azure subscription for Key Vault
- SQL Server instance for testing

## Implementation Steps

### Step 1: Create Interface Definition

Create the following interface in the Infrastructure project:

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Data/IDbConnectionFactory.cs`

```csharp
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
        Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            string databaseName,
            CancellationToken cancellationToken = default);
    }
}
```
### Step 2: Create Connection Options Configuration

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Configuration/DatabaseOptions.cs`

```csharp
namespace DatabaseAutomationPlatform.Infrastructure.Configuration
{
    /// <summary>
    /// Database connection configuration options
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";
        
        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;
        
        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        public int CommandTimeout { get; set; } = 30;
        
        /// <summary>
        /// Maximum retry attempts for transient failures
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>
        /// Delay between retries in milliseconds
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;
        
        /// <summary>
        /// Enable connection pooling
        /// </summary>
        public bool EnableConnectionPooling { get; set; } = true;
        
        /// <summary>
        /// Minimum pool size
        /// </summary>
        public int MinPoolSize { get; set; } = 5;
        
        /// <summary>
        /// Maximum pool size
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;
    }
}
```
### Step 3: Implement Secure Connection String Provider

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Security/ISecureConnectionStringProvider.cs`

```csharp
namespace DatabaseAutomationPlatform.Infrastructure.Security
{
    /// <summary>
    /// Provides secure connection strings from Azure Key Vault
    /// </summary>
    public interface ISecureConnectionStringProvider
    {
        /// <summary>
        /// Retrieves a connection string securely from Key Vault
        /// </summary>
        Task<string> GetConnectionStringAsync(
            string connectionName, 
            CancellationToken cancellationToken = default);
    }
}
```

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Security/SecureConnectionStringProvider.cs`

```csharp
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
    /// Secure connection string provider with Key Vault integration
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
            var keyVaultUrl = _configuration["AzureKeyVault:VaultUrl"] 
                ?? throw new InvalidOperationException("Azure Key Vault URL not configured");
                
            _secretClient = new SecretClient(
                new Uri(keyVaultUrl), 
                new DefaultAzureCredential());

            // Configure retry policy for transient failures
            _retryPolicy = Policy
                .Handle<Azure.RequestFailedException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {TimeSpan}s due to: {Message}",
                            retryCount, timeSpan.TotalSeconds, exception.Message);
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
                _logger.LogInformation("Retrieving connection string from Key Vault for {ConnectionName}", connectionName);
                
                // Try configuration first (for local development)
                var connectionString = _configuration.GetConnectionString(connectionName);
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    // Retrieve from Key Vault
                    var secretName = $"ConnectionString-{connectionName}";
                    var response = await _retryPolicy.ExecuteAsync(async () =>
                        await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken));
                    
                    connectionString = response.Value.Value;
                }

                // Validate connection string
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new SecurityException($"Connection string '{connectionName}' not found");

                // Cache the connection string
                _cache.Set(cacheKey, connectionString, TimeSpan.FromMinutes(CacheExpirationMinutes));
                
                _logger.LogInformation("Successfully retrieved connection string for {ConnectionName}", connectionName);
                return connectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve connection string for {ConnectionName}", connectionName);
                throw new SecurityException($"Failed to retrieve secure connection string: {connectionName}", ex);
            }
        }
    }
}
```
### Step 4: Implement SQL Connection Factory

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Data/SqlConnectionFactory.cs`

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Data;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Infrastructure.Logging;

namespace DatabaseAutomationPlatform.Infrastructure.Data
{
    /// <summary>
    /// Secure SQL Server connection factory with retry policies
    /// </summary>
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly ISecureConnectionStringProvider _connectionStringProvider;
        private readonly ILogger<SqlConnectionFactory> _logger;
        private readonly DatabaseOptions _options;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ISecurityLogger _securityLogger;

        // SQL error numbers for transient failures
        private static readonly int[] TransientErrorNumbers = new[]
        {
            49918, // Cannot process request. Not enough resources to process request
            49919, // Cannot process create or update request. Too many operations in progress
            49920, // Cannot process request. Too many operations in progress
            4060,  // Cannot open database requested by the login
            40143, // The service has encountered an error processing your request
            1205,  // Deadlock victim
            40197, // The service has encountered an error processing your request
            40501, // The service is currently busy
            40613, // Database is currently unavailable
            49919, // Cannot process create or update request
            233,   // Connection initialization error
            64     // A connection was successfully established with the server
        };
    }
}
``````csharp
        public SqlConnectionFactory(
            ISecureConnectionStringProvider connectionStringProvider,
            ILogger<SqlConnectionFactory> logger,
            IOptions<DatabaseOptions> options,
            ISecurityLogger securityLogger)
        {
            _connectionStringProvider = connectionStringProvider ?? 
                throw new ArgumentNullException(nameof(connectionStringProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));

            // Configure retry policy for transient SQL failures
            _retryPolicy = Policy
                .Handle<SqlException>(IsTransientError)
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    _options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromMilliseconds(
                        _options.RetryDelayMilliseconds * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var exception = outcome.Exception;
                        _logger.LogWarning(
                            "Retry {RetryCount} after {Delay}ms due to: {Message}",
                            retryCount,
                            timespan.TotalMilliseconds,
                            exception.Message);
                    });
        }

        private static bool IsTransientError(SqlException sqlException)
        {
            foreach (SqlError error in sqlException.Errors)
            {
                if (TransientErrorNumbers.Contains(error.Number))
                    return true;
            }
            return false;
        }
``````csharp
        public async Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            CancellationToken cancellationToken = default)
        {
            return await CreateConnectionAsync(connectionName, null, cancellationToken);
        }

        public async Task<IDbConnection> CreateConnectionAsync(
            string connectionName,
            string databaseName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

            using var activity = Diagnostics.ActivitySource.StartActivity("CreateDatabaseConnection");
            activity?.SetTag("connection.name", connectionName);
            activity?.SetTag("database.name", databaseName);

            try
            {
                _logger.LogDebug("Creating connection for {ConnectionName}", connectionName);

                // Get secure connection string
                var baseConnectionString = await _connectionStringProvider.GetConnectionStringAsync(
                    connectionName, cancellationToken);

                // Build connection string with security options
                var builder = new SqlConnectionStringBuilder(baseConnectionString)
                {
                    ConnectTimeout = _options.ConnectionTimeout,
                    Encrypt = true,
                    TrustServerCertificate = false,
                    IntegratedSecurity = false,
                    MultipleActiveResultSets = false,
                    ApplicationName = "DatabaseAutomationPlatform",
                    Pooling = _options.EnableConnectionPooling,
                    MinPoolSize = _options.MinPoolSize,
                    MaxPoolSize = _options.MaxPoolSize
                };

                // Override database if specified
                if (!string.IsNullOrWhiteSpace(databaseName))
                {
                    builder.InitialCatalog = databaseName;
                }
``````csharp
                // Create and open connection with retry policy
                var connection = new SqlConnection(builder.ConnectionString);
                
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await connection.OpenAsync(cancellationToken);
                    
                    // Validate connection
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT @@VERSION";
                    command.CommandTimeout = 5;
                    await command.ExecuteScalarAsync(cancellationToken);
                });

                // Log successful connection (without sensitive data)
                _logger.LogInformation(
                    "Successfully created connection to {DatabaseName} on {DataSource}",
                    connection.Database,
                    MaskServerName(connection.DataSource));

                // Security audit log
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{databaseName ?? "default"}",
                    Success = true,
                    IpAddress = GetClientIpAddress(),
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["Database"] = connection.Database,
                        ["ServerVersion"] = connection.ServerVersion
                    }
                });

                activity?.SetStatus(ActivityStatusCode.Ok);
                return connection;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _logger.LogError(ex, 
                    "Failed to create connection for {ConnectionName}", 
                    connectionName);
``````csharp
                // Security audit log for failures
                await _securityLogger.LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.DatabaseConnection,
                    UserId = Thread.CurrentPrincipal?.Identity?.Name ?? "System",
                    ResourceName = $"{connectionName}/{databaseName ?? "default"}",
                    Success = false,
                    IpAddress = GetClientIpAddress(),
                    ErrorMessage = ex.Message,
                    Details = new Dictionary<string, object>
                    {
                        ["ConnectionName"] = connectionName,
                        ["ExceptionType"] = ex.GetType().Name
                    }
                });

                // Don't expose internal details in exceptions
                if (ex is SqlException sqlEx && IsTransientError(sqlEx))
                {
                    throw new DataException(
                        "Temporary database connection issue. Please try again.", 
                        ex);
                }
                
                throw new DataException(
                    "Unable to establish database connection. Please contact support.", 
                    ex);
            }
        }

        private static string MaskServerName(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
                return "unknown";

            var parts = dataSource.Split(',', '\\');
            if (parts.Length == 0)
                return "***";

            var serverName = parts[0];
            if (serverName.Length <= 4)
                return "***";

            return serverName.Substring(0, 2) + "***" + serverName.Substring(serverName.Length - 2);
        }

        private static string GetClientIpAddress()
        {
            // In a web context, this would get the actual client IP
            // For now, return the machine name
            return Environment.MachineName;
        }
    }
}
```
### Step 5: Implement Security Logger

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/Logging/ISecurityLogger.cs`

```csharp
namespace DatabaseAutomationPlatform.Infrastructure.Logging
{
    public interface ISecurityLogger
    {
        Task LogSecurityEventAsync(SecurityEvent securityEvent);
    }

    public class SecurityEvent
    {
        public SecurityEventType EventType { get; set; }
        public string UserId { get; set; }
        public string ResourceName { get; set; }
        public bool Success { get; set; }
        public string IpAddress { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum SecurityEventType
    {
        DatabaseConnection,
        Authentication,
        Authorization,
        DataAccess,
        ConfigurationChange,
        SecurityException
    }
}
```
### Step 6: Configure Dependency Injection

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/ServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Logging;

namespace DatabaseAutomationPlatform.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Configure options
            services.Configure<DatabaseOptions>(
                configuration.GetSection(DatabaseOptions.SectionName));
            
            // Register services
            services.AddMemoryCache();
            services.AddSingleton<ISecureConnectionStringProvider, SecureConnectionStringProvider>();
            services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<ISecurityLogger, SecurityLogger>();
            
            // Add health checks
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database")
                .AddCheck<KeyVaultHealthCheck>("keyvault");
                
            return services;
        }
    }
}
```
### Step 7: Configure appsettings.json

**Location**: `src/DatabaseAutomationPlatform.Api/appsettings.json`

```json
{
  "Database": {
    "ConnectionTimeout": 30,
    "CommandTimeout": 30,
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000,
    "EnableConnectionPooling": true,
    "MinPoolSize": 5,
    "MaxPoolSize": 100
  },
  "AzureKeyVault": {
    "VaultUrl": "https://your-keyvault.vault.azure.net/"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(local);Database=DatabaseAutomation;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
  }
}
```

### Step 8: Create Unit Tests

**Location**: `tests/DatabaseAutomationPlatform.Infrastructure.Tests/Data/SqlConnectionFactoryTests.cs`

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Logging;
namespace DatabaseAutomationPlatform.Infrastructure.Tests.Data
{
    public class SqlConnectionFactoryTests
    {
        private readonly Mock<ISecureConnectionStringProvider> _mockConnectionProvider;
        private readonly Mock<ILogger<SqlConnectionFactory>> _mockLogger;
        private readonly Mock<ISecurityLogger> _mockSecurityLogger;
        private readonly IOptions<DatabaseOptions> _options;
        private readonly SqlConnectionFactory _factory;

        public SqlConnectionFactoryTests()
        {
            _mockConnectionProvider = new Mock<ISecureConnectionStringProvider>();
            _mockLogger = new Mock<ILogger<SqlConnectionFactory>>();
            _mockSecurityLogger = new Mock<ISecurityLogger>();
            _options = Options.Create(new DatabaseOptions
            {
                ConnectionTimeout = 30,
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 100
            });

            _factory = new SqlConnectionFactory(
                _mockConnectionProvider.Object,
                _mockLogger.Object,
                _options,
                _mockSecurityLogger.Object);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithValidConnectionString_ReturnsOpenConnection()
        {
            // Arrange
            const string connectionName = "TestConnection";
            const string connectionString = "Server=(local);Database=Test;Integrated Security=true";
            
            _mockConnectionProvider
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectionString);

            // Act
            var connection = await _factory.CreateConnectionAsync(connectionName);

            // Assert
            connection.Should().NotBeNull();
            connection.State.Should().Be(ConnectionState.Open);
            
            // Verify security logging
            _mockSecurityLogger.Verify(x => x.LogSecurityEventAsync(
                It.Is<SecurityEvent>(e => e.Success == true && e.EventType == SecurityEventType.DatabaseConnection)),
                Times.Once);
        }
        [Fact]
        public async Task CreateConnectionAsync_WithInvalidConnectionString_ThrowsException()
        {
            // Arrange
            const string connectionName = "InvalidConnection";
            
            _mockConnectionProvider
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SecurityException("Connection string not found"));

            // Act & Assert
            await Assert.ThrowsAsync<SecurityException>(
                () => _factory.CreateConnectionAsync(connectionName));
                
            // Verify security logging for failure
            _mockSecurityLogger.Verify(x => x.LogSecurityEventAsync(
                It.Is<SecurityEvent>(e => e.Success == false && e.EventType == SecurityEventType.DatabaseConnection)),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateConnectionAsync_WithInvalidConnectionName_ThrowsArgumentException(string connectionName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _factory.CreateConnectionAsync(connectionName));
        }
    }
}
```

### Step 9: Create Health Check

**Location**: `src/DatabaseAutomationPlatform.Infrastructure/HealthChecks/DatabaseHealthCheck.cs````csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data.Common;

namespace DatabaseAutomationPlatform.Infrastructure.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(
            IDbConnectionFactory connectionFactory,
            ILogger<DatabaseHealthCheck> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(
                    "DefaultConnection", cancellationToken);
                    
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
                
                return HealthCheckResult.Healthy("Database connection is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy(
                    "Database connection failed", 
                    ex, 
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
```
## Security Considerations

### 1. Connection String Security
- Never store connection strings in code
- Use Azure Key Vault for production
- Rotate connection strings regularly
- Use managed identities when possible

### 2. Network Security
- Always use encrypted connections (Encrypt=true)
- Use private endpoints for Azure SQL
- Implement IP whitelisting
- Use VNet integration

### 3. Authentication
- Prefer Azure AD authentication
- Avoid SQL authentication when possible
- Use service principals for applications
- Implement least privilege access

### 4. Monitoring
- Log all connection attempts
- Monitor failed connections
- Alert on unusual patterns
- Track connection pool usage

## Deployment Checklist

- [ ] Azure Key Vault configured with connection strings
- [ ] Managed identity assigned and permissions granted
- [ ] Network security rules configured
- [ ] Connection pool settings optimized
- [ ] Health checks endpoint exposed
- [ ] Monitoring and alerts configured
- [ ] Security logging enabled
- [ ] Retry policies tested
- [ ] Performance benchmarks met
- [ ] Documentation updated

## Troubleshooting

### Common Issues

1. **Connection Timeout**
   - Check network connectivity
   - Verify firewall rules
   - Increase connection timeout

2. **Authentication Failures**
   - Verify Azure AD configuration
   - Check service principal permissions
   - Validate connection string format
3. **Transient Errors**
   - Retry policy should handle automatically
   - Check for resource throttling
   - Monitor Azure SQL DTU/vCore usage

4. **Connection Pool Exhaustion**
   - Increase MaxPoolSize if needed
   - Ensure connections are disposed properly
   - Monitor pool usage metrics

## Performance Optimization

1. **Connection Pooling**
   - Enable for better performance
   - Set appropriate pool size limits
   - Monitor pool statistics

2. **Retry Strategy**
   - Use exponential backoff
   - Limit retry attempts
   - Log retry attempts

3. **Caching**
   - Cache connection strings
   - Implement cache expiration
   - Monitor cache hit rates

## Next Steps

After implementing the connection factory:

1. Implement IStoredProcedureExecutor
2. Create database migration scripts
3. Set up integration tests
4. Configure CI/CD pipeline
5. Document API usage

## References

- [Azure SQL Connection Best Practices](https://docs.microsoft.com/azure/sql-database/sql-database-connectivity-issues)
- [Key Vault Configuration](https://docs.microsoft.com/azure/key-vault/)
- [Polly Retry Policies](https://github.com/App-vNext/Polly)
- [SQL Server Connection Pooling](https://docs.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling)

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-20  
**Author**: Database Automation Platform Team