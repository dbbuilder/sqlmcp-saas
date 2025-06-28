using DatabaseAutomationPlatform.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Encodings.Web;
using Xunit;

namespace DatabaseAutomationPlatform.Api.Tests.Middleware;

public class ApiKeyAuthenticationTests
{
    private readonly Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>> _optionsMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<UrlEncoder> _encoderMock;
    private readonly Mock<ISystemClock> _clockMock;
    private readonly Mock<IApiKeyValidationService> _apiKeyServiceMock;
    private readonly ApiKeyAuthenticationHandler _handler;
    private readonly DefaultHttpContext _context;

    public ApiKeyAuthenticationTests()
    {
        _optionsMock = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        _optionsMock.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new ApiKeyAuthenticationOptions());

        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger<ApiKeyAuthenticationHandler>>().Object);

        _encoderMock = new Mock<UrlEncoder>();
        _clockMock = new Mock<ISystemClock>();
        _apiKeyServiceMock = new Mock<IApiKeyValidationService>();

        _handler = new ApiKeyAuthenticationHandler(
            _optionsMock.Object,
            _loggerFactoryMock.Object,
            _encoderMock.Object,
            _apiKeyServiceMock.Object);

        _context = new DefaultHttpContext();
        _handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationOptions.DefaultScheme, null, typeof(ApiKeyAuthenticationHandler)),
            _context).Wait();
    }

    [Fact]
    public async Task AuthenticateAsync_NoApiKeyHeader_ReturnsNoResult()
    {
        // Arrange
        // No API key header added

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyApiKey_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["X-API-Key"] = "";

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidApiKey_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["X-API-Key"] = "invalid-key";
        
        _apiKeyServiceMock.Setup(x => x.ValidateApiKeyAsync("invalid-key"))
            .ReturnsAsync(new ApiKeyValidationResult { IsValid = false });

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task AuthenticateAsync_ValidApiKey_ReturnsSuccess()
    {
        // Arrange
        _context.Request.Headers["X-API-Key"] = "valid-key";
        
        var validationResult = new ApiKeyValidationResult
        {
            IsValid = true,
            ApiKeyId = "key1",
            ClientId = "client1",
            ClientName = "Test Client",
            Roles = new[] { "Developer", "Viewer" },
            Permissions = new[] { "query:execute", "schema:read" }
        };
        
        _apiKeyServiceMock.Setup(x => x.ValidateApiKeyAsync("valid-key"))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
        result.Principal.HasClaim("client_id", "client1").Should().BeTrue();
        result.Principal.HasClaim("role", "Developer").Should().BeTrue();
        result.Principal.HasClaim("role", "Viewer").Should().BeTrue();
        result.Principal.HasClaim("permission", "query:execute").Should().BeTrue();
        result.Principal.HasClaim("permission", "schema:read").Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_ExpiredApiKey_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["X-API-Key"] = "expired-key";
        
        var validationResult = new ApiKeyValidationResult
        {
            IsValid = true,
            ApiKeyId = "key1",
            ClientId = "client1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Expired yesterday
        };
        
        _apiKeyServiceMock.Setup(x => x.ValidateApiKeyAsync("expired-key"))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }
}

public class ApiKeyValidationServiceTests
{
    private readonly Mock<ILogger<ApiKeyValidationService>> _loggerMock;
    private readonly ApiKeyValidationService _sut;

    public ApiKeyValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ApiKeyValidationService>>();
        _sut = new ApiKeyValidationService(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_KnownKey_ReturnsValid()
    {
        // Arrange
        var apiKey = "demo-api-key-12345";

        // Act
        var result = await _sut.ValidateApiKeyAsync(apiKey);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ClientId.Should().Be("demo-client");
        result.Roles.Should().Contain("Developer");
        result.Permissions.Should().Contain("query:execute");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_UnknownKey_ReturnsInvalid()
    {
        // Arrange
        var apiKey = "unknown-key";

        // Act
        var result = await _sut.ValidateApiKeyAsync(apiKey);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_EmptyKey_ReturnsInvalid()
    {
        // Arrange
        var apiKey = "";

        // Act
        var result = await _sut.ValidateApiKeyAsync(apiKey);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateApiKeyAsync_ReturnsNewKey()
    {
        // Arrange
        var clientId = "test-client";
        var roles = new[] { "Developer" };
        var permissions = new[] { "query:execute" };

        // Act
        var apiKey = await _sut.GenerateApiKeyAsync(clientId, roles, permissions);

        // Assert
        apiKey.Should().NotBeNullOrEmpty();
        apiKey.Should().StartWith("test-client-");
    }

    [Fact]
    public async Task RevokeApiKeyAsync_RemovesKey()
    {
        // Arrange
        var apiKey = "demo-api-key-12345";

        // Act
        await _sut.RevokeApiKeyAsync(apiKey);

        // Assert
        // Verify the key is no longer valid
        var result = await _sut.ValidateApiKeyAsync(apiKey);
        result.IsValid.Should().BeFalse();
    }
}