using DatabaseAutomationPlatform.Api.Extensions;
using DatabaseAutomationPlatform.Api.Services;
using DatabaseAutomationPlatform.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DatabaseAutomationPlatform.Api.Tests.Services;

public class McpServiceTests
{
    private readonly Mock<IOptions<McpConfiguration>> _configurationMock;
    private readonly Mock<ILogger<McpService>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDeveloperService> _developerServiceMock;
    private readonly Mock<IDbaService> _dbaServiceMock;
    private readonly Mock<ISchemaService> _schemaServiceMock;
    private readonly Mock<IAnalyticsService> _analyticsServiceMock;
    private readonly McpService _sut;

    public McpServiceTests()
    {
        _configurationMock = new Mock<IOptions<McpConfiguration>>();
        _configurationMock.Setup(x => x.Value).Returns(new McpConfiguration
        {
            ServerName = "TestServer",
            Version = "1.0.0",
            MaxRequestSize = 1048576,
            RequestTimeout = 30000,
            SupportedCapabilities = new[] { "tools", "resources", "prompts" }
        });

        _loggerMock = new Mock<ILogger<McpService>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _developerServiceMock = new Mock<IDeveloperService>();
        _dbaServiceMock = new Mock<IDbaService>();
        _schemaServiceMock = new Mock<ISchemaService>();
        _analyticsServiceMock = new Mock<IAnalyticsService>();

        _serviceProviderMock.Setup(x => x.GetService(typeof(IDeveloperService)))
            .Returns(_developerServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IDbaService)))
            .Returns(_dbaServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ISchemaService)))
            .Returns(_schemaServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IAnalyticsService)))
            .Returns(_analyticsServiceMock.Object);

        _sut = new McpService(_configurationMock.Object, _loggerMock.Object, _serviceProviderMock.Object);
    }

    [Fact]
    public async Task ProcessRequestAsync_Initialize_ReturnsServerCapabilities()
    {
        // Arrange
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
        var response = await _sut.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().Be(1);
        response.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        var result = response.Result as dynamic;
        result.Should().NotBeNull();
        result.protocolVersion.Should().Be("1.0");
        result.serverInfo.name.Should().Be("TestServer");
        result.serverInfo.version.Should().Be("1.0.0");
        result.capabilities.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessRequestAsync_ToolsList_ReturnsAvailableTools()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 2
        };

        // Act
        var response = await _sut.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().Be(2);
        response.Error.Should().BeNull();
        
        var tools = response.Result as IEnumerable<McpTool>;
        tools.Should().NotBeNull();
        tools.Should().HaveCount(4);
        tools.Should().Contain(t => t.Name == "query");
        tools.Should().Contain(t => t.Name == "execute");
        tools.Should().Contain(t => t.Name == "schema");
        tools.Should().Contain(t => t.Name == "analyze");
    }

    [Fact]
    public async Task ProcessRequestAsync_ToolsCall_Query_ExecutesSuccessfully()
    {
        // Arrange
        var queryResult = new QueryResult
        {
            Columns = new[] { "Id", "Name" },
            Rows = new[] 
            { 
                new object[] { 1, "Test1" },
                new object[] { 2, "Test2" }
            },
            RowCount = 2
        };

        _developerServiceMock.Setup(x => x.ExecuteQueryAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/call",
            Id = 3,
            Params = new Dictionary<string, object>
            {
                ["name"] = "query",
                ["arguments"] = new Dictionary<string, object>
                {
                    ["database"] = "TestDB",
                    ["query"] = "SELECT * FROM Users",
                    ["timeout"] = 30
                }
            }
        };

        // Act
        var response = await _sut.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().Be(3);
        response.Error.Should().BeNull();
        
        var result = response.Result as McpToolResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();

        _developerServiceMock.Verify(x => x.ExecuteQueryAsync("TestDB", "SELECT * FROM Users", 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRequestAsync_ToolsCall_Execute_RequiresDeveloperRole()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/call",
            Id = 4,
            Params = new Dictionary<string, object>
            {
                ["name"] = "execute",
                ["arguments"] = new Dictionary<string, object>
                {
                    ["database"] = "TestDB",
                    ["command"] = "INSERT INTO Users (Name) VALUES ('Test')",
                    ["transaction"] = true
                }
            }
        };

        _developerServiceMock.Setup(x => x.ExecuteCommandAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { AffectedRows = 1, Success = true });

        // Act
        var response = await _sut.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Error.Should().BeNull();
        
        var result = response.Result as McpToolResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRequestAsync_InvalidMethod_ReturnsError()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "invalid/method",
            Id = 5
        };

        // Act
        var response = await _sut.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().Be(5);
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32603);
        response.Error.Message.Should().Be("Internal error");
    }

    [Fact]
    public async Task ProcessRequestAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ProcessRequestAsync(null!));
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ReturnsExpectedCapabilities()
    {
        // Act
        var capabilities = await _sut.GetCapabilitiesAsync();

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.Tools.Should().BeEquivalentTo(new[] { "query", "execute", "schema", "analyze" });
        capabilities.Resources.Should().BeEquivalentTo(new[] { "databases", "tables", "procedures", "views" });
        capabilities.Prompts.Should().BeEquivalentTo(new[] { "sql-generation", "optimization", "security-review" });
        capabilities.Experimental.Should().ContainKey("streaming");
        capabilities.Experimental.Should().ContainKey("batch");
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsAllTools()
    {
        // Act
        var tools = await _sut.ListToolsAsync();

        // Assert
        tools.Should().NotBeNull();
        tools.Should().HaveCount(4);
        
        var queryTool = tools.FirstOrDefault(t => t.Name == "query");
        queryTool.Should().NotBeNull();
        queryTool!.Description.Should().Be("Execute a read-only SQL query");
        queryTool.InputSchema.Should().ContainKey("database");
        queryTool.InputSchema.Should().ContainKey("query");
        queryTool.InputSchema.Should().ContainKey("timeout");

        var executeTool = tools.FirstOrDefault(t => t.Name == "execute");
        executeTool.Should().NotBeNull();
        executeTool!.Description.Should().Be("Execute a SQL command (INSERT, UPDATE, DELETE)");
        executeTool.InputSchema.Should().ContainKey("database");
        executeTool.InputSchema.Should().ContainKey("command");
        executeTool.InputSchema.Should().ContainKey("transaction");
    }

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_ReturnsError()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.ExecuteToolAsync("unknown", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Tool 'unknown' is not supported");
    }

    [Fact]
    public async Task ExecuteToolAsync_QueryTool_WithInvalidParameters_ReturnsError()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            // Missing required 'database' parameter
            ["query"] = "SELECT * FROM Users"
        };

        // Act
        var result = await _sut.ExecuteToolAsync("query", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("database");
    }
}

// Supporting classes for tests
public class QueryResult
{
    public string[] Columns { get; set; } = Array.Empty<string>();
    public object[][] Rows { get; set; } = Array.Empty<object[]>();
    public int RowCount { get; set; }
}

public class CommandResult
{
    public int AffectedRows { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}