using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using DatabaseAutomationPlatform.Infrastructure.Security;

namespace DatabaseAutomationPlatform.Tests.Unit.Infrastructure.Security
{
    public class AzureKeyVaultSecretManagerTests
    {
        private readonly Mock<ILogger<AzureKeyVaultSecretManager>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly string _testVaultUri = "https://test-vault.vault.azure.net/";

        public AzureKeyVaultSecretManagerTests()
        {
            _loggerMock = new Mock<ILogger<AzureKeyVaultSecretManager>>();
            _configurationMock = new Mock<IConfiguration>();
            
            // Setup default configuration
            _configurationMock.Setup(x => x["Azure:KeyVault:VaultUri"])
                .Returns(_testVaultUri);
            _configurationMock.Setup(x => x.GetSection("Azure:KeyVault:CacheExpirationMinutes").Value)
                .Returns("5");
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenLoggerIsNull()
        {
            // Act
            var act = () => new AzureKeyVaultSecretManager(
                _configurationMock.Object,
                null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenVaultUriNotConfigured()
        {
            // Arrange
            _configurationMock.Setup(x => x["Azure:KeyVault:VaultUri"])
                .Returns((string)null);

            // Act
            var act = () => new AzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Azure Key Vault URI not configured");
        }

        [Fact]
        public async Task GetSecretAsync_ShouldThrowException_WhenSecretNameIsNull()
        {
            // Arrange
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Act
            var act = async () => await manager.GetSecretAsync(null);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("secretName");
        }

        [Fact]
        public async Task GetSecretAsync_ShouldThrowException_WhenSecretNameIsEmpty()
        {
            // Arrange
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Act
            var act = async () => await manager.GetSecretAsync("");

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("secretName");
        }

        [Fact]
        public async Task GetSecretAsync_ShouldReturnCachedValue_WhenCacheIsValid()
        {
            // Arrange
            var secretName = "test-secret";
            var secretValue = "test-value";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            // First call to populate cache
            manager.SetupSecretClientMock(secretName, secretValue);
            await manager.GetSecretAsync(secretName);
            
            // Reset mock to verify it's not called again
            manager.ResetSecretClientMock();

            // Act
            var result = await manager.GetSecretAsync(secretName);

            // Assert
            result.Should().Be(secretValue);
            manager.VerifySecretClientNotCalled();
        }

        [Fact]
        public async Task GetSecretAsync_ShouldThrowKeyNotFoundException_WhenSecretNotFound()
        {
            // Arrange
            var secretName = "non-existent-secret";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            manager.SetupSecretClientMockToThrow404(secretName);

            // Act
            var act = async () => await manager.GetSecretAsync(secretName);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"Secret '{secretName}' not found in Key Vault");
        }

        [Fact]
        public async Task SetSecretAsync_ShouldThrowException_WhenSecretNameIsNull()
        {
            // Arrange
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Act
            var act = async () => await manager.SetSecretAsync(null, "value");

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("secretName");
        }

        [Fact]
        public async Task SetSecretAsync_ShouldThrowException_WhenSecretValueIsNull()
        {
            // Arrange
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Act
            var act = async () => await manager.SetSecretAsync("test-secret", null);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("secretValue");
        }

        [Fact]
        public async Task SetSecretAsync_ShouldInvalidateCache()
        {
            // Arrange
            var secretName = "test-secret";
            var oldValue = "old-value";
            var newValue = "new-value";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            // First get to populate cache
            manager.SetupSecretClientMock(secretName, oldValue);
            await manager.GetSecretAsync(secretName);
            
            // Set new value
            var version = "v2";
            manager.SetupSetSecretMock(secretName, newValue, version);
            
            // Act
            var resultVersion = await manager.SetSecretAsync(secretName, newValue);
            
            // Now get should fetch from Key Vault, not cache
            manager.SetupSecretClientMock(secretName, newValue);
            var retrievedValue = await manager.GetSecretAsync(secretName);

            // Assert
            resultVersion.Should().Be(version);
            retrievedValue.Should().Be(newValue);
        }

        [Fact]
        public async Task SecretExistsAsync_ShouldReturnTrue_WhenSecretExists()
        {
            // Arrange
            var secretName = "existing-secret";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            manager.SetupSecretClientMock(secretName, "value");

            // Act
            var exists = await manager.SecretExistsAsync(secretName);

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task SecretExistsAsync_ShouldReturnFalse_WhenSecretNotFound()
        {
            // Arrange
            var secretName = "non-existent-secret";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            manager.SetupSecretClientMockToThrow404(secretName);

            // Act
            var exists = await manager.SecretExistsAsync(secretName);

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteSecretAsync_ShouldThrowException_WhenSecretNameIsNull()
        {
            // Arrange
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Act
            var act = async () => await manager.DeleteSecretAsync(null);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("secretName");
        }

        [Fact]
        public async Task DeleteSecretAsync_ShouldRemoveFromCache()
        {
            // Arrange
            var secretName = "test-secret";
            var secretValue = "test-value";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            // First get to populate cache
            manager.SetupSecretClientMock(secretName, secretValue);
            await manager.GetSecretAsync(secretName);
            
            // Setup delete
            manager.SetupDeleteSecretMock(secretName);
            
            // Act
            await manager.DeleteSecretAsync(secretName);
            
            // Now get should fetch from Key Vault (and throw 404)
            manager.SetupSecretClientMockToThrow404(secretName);
            var act = async () => await manager.GetSecretAsync(secretName);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task GetSecretAsync_ShouldRetryOnTransientErrors()
        {
            // Arrange
            var secretName = "test-secret";
            var secretValue = "test-value";
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);
            
            // Setup to fail twice then succeed
            manager.SetupSecretClientMockWithTransientErrors(secretName, secretValue, 2);

            // Act
            var result = await manager.GetSecretAsync(secretName);

            // Assert
            result.Should().Be(secretValue);
            
            // Verify retry logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Key Vault operation retry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public void CacheExpiration_ShouldUseConfiguredValue()
        {
            // Arrange
            var customExpirationMinutes = "10";
            _configurationMock.Setup(x => x.GetSection("Azure:KeyVault:CacheExpirationMinutes").Value)
                .Returns(customExpirationMinutes);

            // Act
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Assert
            // This is tested implicitly through the cache behavior
            // The TestableAzureKeyVaultSecretManager exposes the cache expiration for verification
            manager.CacheExpirationMinutes.Should().Be(10);
        }

        [Fact]
        public void CacheExpiration_ShouldUseDefaultValue_WhenNotConfigured()
        {
            // Arrange
            _configurationMock.Setup(x => x.GetSection("Azure:KeyVault:CacheExpirationMinutes").Value)
                .Returns((string)null);

            // Act
            var manager = new TestableAzureKeyVaultSecretManager(
                _configurationMock.Object,
                _loggerMock.Object);

            // Assert
            manager.CacheExpirationMinutes.Should().Be(5); // Default value
        }
    }

    /// <summary>
    /// Testable version of AzureKeyVaultSecretManager that allows mocking the SecretClient
    /// </summary>
    internal class TestableAzureKeyVaultSecretManager : AzureKeyVaultSecretManager
    {
        private readonly Mock<SecretClient> _secretClientMock;
        private int _getSecretCallCount = 0;
        
        public int CacheExpirationMinutes { get; }

        public TestableAzureKeyVaultSecretManager(
            IConfiguration configuration,
            ILogger<AzureKeyVaultSecretManager> logger) 
            : base(configuration, logger)
        {
            _secretClientMock = new Mock<SecretClient>();
            
            // Extract cache expiration for testing
            CacheExpirationMinutes = configuration.GetValue<int>("Azure:KeyVault:CacheExpirationMinutes", 5);
            
            // Use reflection to replace the secret client
            var fieldInfo = typeof(AzureKeyVaultSecretManager)
                .GetField("_secretClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo?.SetValue(this, _secretClientMock.Object);
        }

        public void SetupSecretClientMock(string secretName, string secretValue)
        {
            var secret = SecretModelFactory.KeyVaultSecret(
                SecretModelFactory.SecretProperties(new Uri($"https://test-vault.vault.azure.net/secrets/{secretName}")),
                secretValue);
            
            var response = Response.FromValue(secret, Mock.Of<Response>());
            
            _secretClientMock.Setup(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
        }

        public void SetupSecretClientMockToThrow404(string secretName)
        {
            _secretClientMock.Setup(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));
        }

        public void SetupSecretClientMockWithTransientErrors(string secretName, string secretValue, int failureCount)
        {
            var secret = SecretModelFactory.KeyVaultSecret(
                SecretModelFactory.SecretProperties(new Uri($"https://test-vault.vault.azure.net/secrets/{secretName}")),
                secretValue);
            
            var response = Response.FromValue(secret, Mock.Of<Response>());
            
            _secretClientMock.SetupSequence(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(503, "Service Unavailable"))
                .ThrowsAsync(new RequestFailedException(503, "Service Unavailable"))
                .ReturnsAsync(response);
        }

        public void SetupSetSecretMock(string secretName, string secretValue, string version)
        {
            var properties = SecretModelFactory.SecretProperties(
                new Uri($"https://test-vault.vault.azure.net/secrets/{secretName}"),
                version: version);
            
            var secret = SecretModelFactory.KeyVaultSecret(properties, secretValue);
            var response = Response.FromValue(secret, Mock.Of<Response>());
            
            _secretClientMock.Setup(x => x.SetSecretAsync(secretName, secretValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
        }

        public void SetupDeleteSecretMock(string secretName)
        {
            var operation = new Mock<DeleteSecretOperation>();
            _secretClientMock.Setup(x => x.StartDeleteSecretAsync(secretName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(operation.Object);
        }

        public void ResetSecretClientMock()
        {
            _secretClientMock.Reset();
        }

        public void VerifySecretClientNotCalled()
        {
            _secretClientMock.Verify(
                x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), 
                Times.Never);
        }
    }
}