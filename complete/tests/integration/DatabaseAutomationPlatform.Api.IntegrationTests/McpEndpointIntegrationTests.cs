using DatabaseAutomationPlatform.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DatabaseAutomationPlatform.Api.IntegrationTests;

public class McpEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public async Task GetCapabilities_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/mcp/capabilities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCapabilities_WithAuth_ReturnsCapabilities()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/mcp/capabilities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var capabilities = await response.Content.ReadFromJsonAsync<McpCapabilities>(_jsonOptions);
        capabilities.Should().NotBeNull();
        capabilities!.Tools.Should().NotBeEmpty();
        capabilities.Resources.Should().NotBeEmpty();
        capabilities.Prompts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListTools_WithAuth_ReturnsTools()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/mcp/tools");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tools = await response.Content.ReadFromJsonAsync<List<McpTool>>(_jsonOptions);
        tools.Should().NotBeNull();
        tools!.Should().HaveCount(4);
        tools.Should().Contain(t => t.Name == "query");
        tools.Should().Contain(t => t.Name == "execute");
        tools.Should().Contain(t => t.Name == "schema");
        tools.Should().Contain(t => t.Name == "analyze");
    }

    [Fact]
    public async Task ProcessRequest_Initialize_ReturnsServerInfo()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "initialize",
            Id = 1,
            Params = new Dictionary<string, object>
            {
                ["clientInfo"] = new { name = "TestClient", version = "1.0" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/mcp", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>(_jsonOptions);
        mcpResponse.Should().NotBeNull();
        mcpResponse!.Id.Should().Be(1);
        mcpResponse.Error.Should().BeNull();
        mcpResponse.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessRequest_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new McpRequest
        {
            JsonRpc = "1.0", // Invalid version
            Method = "initialize",
            Id = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/mcp", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
        errorResponse.Should().NotBeNull();
        errorResponse!.Status.Should().Be(400);
        errorResponse.Errors.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteTool_Query_WithInvalidParameters_ReturnsBadRequest()
    {
        // Arrange
        await AuthenticateAsync();
        var parameters = new Dictionary<string, object>
        {
            // Missing required 'database' parameter
            ["query"] = "SELECT * FROM Users"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/mcp/tools/query", parameters, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
        errorResponse.Should().NotBeNull();
        errorResponse!.Detail.Should().Contain("parameters failed validation");
    }

    [Fact]
    public async Task ProcessRequest_ToolsList_ReturnsTools()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 2
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/mcp", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>(_jsonOptions);
        mcpResponse.Should().NotBeNull();
        mcpResponse!.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task ProcessRequest_WithCorrelationId_ReturnsCorrelationIdInResponse()
    {
        // Arrange
        await AuthenticateAsync();
        var correlationId = Guid.NewGuid().ToString();
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 3
        };

        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/mcp", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().Contain(h => h.Key == "X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").Should().Contain(correlationId);
    }

    private async Task AuthenticateAsync()
    {
        var loginRequest = new
        {
            Username = "demo",
            Password = "demo123"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/token", loginRequest, _jsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);
        tokenResponse.Should().NotBeNull();

        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", tokenResponse!.AccessToken);
    }
}

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}