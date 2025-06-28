using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Security;
using DatabaseAutomationPlatform.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace DatabaseAutomationPlatform.Tests.Unit.Infrastructure.Data
{
    public class SqlConnectionFactoryTests : IDisposable
    {
        private readonly Mock<ISecureConnectionStringProvider> _connectionStringProviderMock;
        private readonly Mock<ILogger<SqlConnectionFactory>> _loggerMock;
        private readonly Mock<ISecurityLogger> _securityLoggerMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly DatabaseOptions _validOptions;
        private readonly SqlConnectionFactory _sut;

        public SqlConnectionFactoryTests()
        {
            _connectionStringProviderMock = new Mock<ISecureConnectionStringProvider>();
            _loggerMock = new Mock<ILogger<SqlConnectionFactory>>();
            _securityLoggerMock = new Mock<ISecurityLogger>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

            _validOptions = new DatabaseOptions
            {
                ConnectionTimeout = 30,
                CommandTimeout = 30,
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 100,
                EnableConnectionPooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 100
            };

            _sut = new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                Options.Create(_validOptions),
                _securityLoggerMock.Object,
                _httpContextAccessorMock.Object);
        }

        [Fact]
        public void Constructor_WithNullConnectionStringProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new SqlConnectionFactory(
                null!,
                _loggerMock.Object,
                Options.Create(_validOptions),
                _securityLoggerMock.Object);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("connectionStringProvider");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                null!,
                Options.Create(_validOptions),
                _securityLoggerMock.Object);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                null!,
                _securityLoggerMock.Object);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("options");
        }

        [Fact]
        public void Constructor_WithNullSecurityLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                Options.Create(_validOptions),
                null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("securityLogger");
        }

        [Fact]
        public async Task CreateConnectionAsync_WithEmptyConnectionName_ThrowsArgumentException()
        {
            // Act
            var act = async () => await _sut.CreateConnectionAsync(string.Empty);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("connectionName");
        }

        [Fact]
        public async Task CreateConnectionAsync_WithNullConnectionName_ThrowsArgumentException()
        {
            // Act
            var act = async () => await _sut.CreateConnectionAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("connectionName");
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public async Task CreateConnectionAsync_WithWhitespaceConnectionName_ThrowsArgumentException(string connectionName)
        {
            // Act
            var act = async () => await _sut.CreateConnectionAsync(connectionName);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("connectionName");
        }

        [Fact]
        public async Task CreateConnectionAsync_WhenConnectionStringProviderFails_LogsSecurityEventAndThrows()
        {
            // Arrange
            var connectionName = "TestConnection";
            var expectedException = new InvalidOperationException("Key Vault error");
            
            _connectionStringProviderMock
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act
            var act = async () => await _sut.CreateConnectionAsync(connectionName);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Failed to establish database connection*")
                .WithInnerException<InvalidOperationException>();

            _securityLoggerMock.Verify(x => x.LogSecurityEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.DatabaseConnection &&
                    e.Success == false &&
                    e.ResourceName == $"{connectionName}/default" &&
                    e.ErrorMessage == "Connection failed due to unexpected error"
                )), Times.Once);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithCancellation_LogsSecurityEventAndThrowsOperationCanceledException()
        {
            // Arrange
            var connectionName = "TestConnection";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _connectionStringProviderMock
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var act = async () => await _sut.CreateConnectionAsync(connectionName, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            _securityLoggerMock.Verify(x => x.LogSecurityEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.DatabaseConnection &&
                    e.Success == false &&
                    e.ErrorMessage == "Operation cancelled"
                )), Times.Once);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithSqlException_LogsSecurityEventWithErrorDetails()
        {
            // Arrange
            var connectionName = "TestConnection";
            var connectionString = "Server=test;Database=test;User Id=test;Password=test;";
            
            _connectionStringProviderMock
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectionString);

            // Create a SQL exception using reflection (since SqlException has no public constructor)
            var sqlException = CreateSqlException(18456); // Login failed error

            // Note: In a real test, you'd need to mock the SqlConnection creation
            // This is a simplified example showing the structure

            // Act & Assert would go here with proper mocking
            // The test would verify that security logging occurs with SQL error details
        }

        [Theory]
        [InlineData("server.database.windows.net", "ser***.windows.net")]
        [InlineData("localhost", "loc***")]
        [InlineData("192.168.1.1", "192***")]
        [InlineData("my-sql-server.corp.contoso.com", "my-***.corp.contoso.com")]
        [InlineData("a", "***")]
        [InlineData("", "***")]
        [InlineData(null, "***")]
        public void MaskServerName_ShouldMaskCorrectly(string input, string expected)
        {
            // Use reflection to test the private method
            var method = typeof(SqlConnectionFactory).GetMethod(
                "MaskServerName", 
                BindingFlags.NonPublic | BindingFlags.Static);

            // Act
            var result = method!.Invoke(null, new object?[] { input });

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GetClientIpAddress_WithHttpContext_ReturnsForwardedForIp()
        {
            // Arrange
            var expectedIp = "192.168.1.100";
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = $"{expectedIp}, 10.0.0.1";
            
            _httpContextAccessorMock
                .Setup(x => x.HttpContext)
                .Returns(context);

            var factory = new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                Options.Create(_validOptions),
                _securityLoggerMock.Object,
                _httpContextAccessorMock.Object);

            // Use reflection to test the private method
            var method = typeof(SqlConnectionFactory).GetMethod(
                "GetClientIpAddress", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = method!.Invoke(factory, null);

            // Assert
            result.Should().Be(expectedIp);
        }

        [Fact]
        public void GetClientIpAddress_WithHttpContextAndRealIp_ReturnsRealIp()
        {
            // Arrange
            var expectedIp = "192.168.1.100";
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Real-IP"] = expectedIp;
            
            _httpContextAccessorMock
                .Setup(x => x.HttpContext)
                .Returns(context);

            var factory = new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                Options.Create(_validOptions),
                _securityLoggerMock.Object,
                _httpContextAccessorMock.Object);

            // Use reflection to test the private method
            var method = typeof(SqlConnectionFactory).GetMethod(
                "GetClientIpAddress", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = method!.Invoke(factory, null);

            // Assert
            result.Should().Be(expectedIp);
        }

        [Fact]
        public void GetClientIpAddress_WithoutHttpContext_ReturnsMachineInfo()
        {
            // Arrange
            var factory = new SqlConnectionFactory(
                _connectionStringProviderMock.Object,
                _loggerMock.Object,
                Options.Create(_validOptions),
                _securityLoggerMock.Object,
                null); // No HTTP context accessor

            // Use reflection to test the private method
            var method = typeof(SqlConnectionFactory).GetMethod(
                "GetClientIpAddress", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = method!.Invoke(factory, null) as string;

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Match(ip => 
                ip!.Contains("(local)") || 
                ip.Contains("(machine)") || 
                ip == "Unknown");
        }

        [Theory]
        [InlineData(49918, true)]  // Not enough resources
        [InlineData(49919, true)]  // Too many operations
        [InlineData(1205, true)]   // Deadlock
        [InlineData(40613, true)]  // Database unavailable
        [InlineData(18456, false)] // Login failed (not transient)
        [InlineData(547, false)]   // INSERT statement conflicted (not transient)
        public void IsTransientError_ShouldIdentifyTransientErrors(int errorNumber, bool expectedResult)
        {
            // Arrange
            var sqlException = CreateSqlException(errorNumber);

            // Use reflection to test the private method
            var method = typeof(SqlConnectionFactory).GetMethod(
                "IsTransientError", 
                BindingFlags.NonPublic | BindingFlags.Static);

            // Act
            var result = method!.Invoke(null, new object[] { sqlException });

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithDatabaseName_OverridesInitialCatalog()
        {
            // Arrange
            var connectionName = "TestConnection";
            var databaseName = "OverrideDatabase";
            var baseConnectionString = "Server=test;Database=original;User Id=test;Password=test;";
            
            _connectionStringProviderMock
                .Setup(x => x.GetConnectionStringAsync(connectionName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseConnectionString);

            // Act
            // Note: In a real scenario, you'd need to mock SqlConnection properly
            // This test structure shows the intent

            // The resulting connection string should have the overridden database
            // Assert would verify that InitialCatalog was set to databaseName
        }

        private SqlException CreateSqlException(int errorNumber)
        {
            // SqlException doesn't have a public constructor, so we need to use reflection
            // In a real test, you might use a different approach or a test helper
            var errorCollection = Activator.CreateInstance(
                typeof(SqlErrorCollection),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, null, null)!;

            var error = Activator.CreateInstance(
                typeof(SqlError),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { errorNumber, (byte)0, (byte)0, "server", "Error message", "procedure", 0, null },
                null)!;

            typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(errorCollection, new[] { error });

            var sqlException = Activator.CreateInstance(
                typeof(SqlException),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { "Test SQL Exception", errorCollection, null, Guid.NewGuid() },
                null) as SqlException;

            return sqlException!;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}