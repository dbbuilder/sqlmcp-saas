using FluentAssertions;
using Xunit;

namespace DatabaseAutomationPlatform.Api.IntegrationTests;

public class ToolExecutionIntegrationTests : IntegrationTestBase
{
    public ToolExecutionIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task QueryTool_ExecutesSuccessfully()
    {
        // Arrange
        await AuthenticateAsync();
        var arguments = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT * FROM Users",
            ["timeout"] = 30
        };

        // Act
        var result = await ExecuteToolAsync<QueryToolResult>("query", arguments);

        // Assert
        result.Should().NotBeNull();
        result!.RowCount.Should().Be(2);
        result.Columns.Should().Contain("Id", "Name", "Email");
        result.ExecutionTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteTool_ExecutesSuccessfully()
    {
        // Arrange
        await AuthenticateAsync();
        var arguments = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["command"] = "UPDATE Users SET LastLogin = GETUTCDATE() WHERE Id = 1",
            ["transaction"] = true
        };

        // Act
        var result = await ExecuteToolAsync<CommandToolResult>("execute", arguments);

        // Assert
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.AffectedRows.Should().Be(1);
        result.ExecutionTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SchemaTool_ExecutesSuccessfully()
    {
        // Arrange
        await AuthenticateAsync();
        var arguments = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["objectType"] = "Table",
            ["objectName"] = "Users"
        };

        // Act
        var result = await ExecuteToolAsync<SchemaToolResult>("schema", arguments);

        // Assert
        result.Should().NotBeNull();
        result!.Tables.Should().ContainKey("Users");
    }

    [Fact]
    public async Task MultipleTool_ExecutionsInSequence()
    {
        // Arrange
        await AuthenticateAsync();

        // Act 1: Query initial state
        var queryResult1 = await ExecuteToolAsync<QueryToolResult>("query", new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT COUNT(*) as UserCount FROM Users"
        });

        // Act 2: Execute an update
        var updateResult = await ExecuteToolAsync<CommandToolResult>("execute", new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["command"] = "INSERT INTO Users (Name, Email) VALUES ('New User', 'new@example.com')",
            ["transaction"] = true
        });

        // Act 3: Query to verify the change
        var queryResult2 = await ExecuteToolAsync<QueryToolResult>("query", new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT COUNT(*) as UserCount FROM Users"
        });

        // Assert
        queryResult1.Should().NotBeNull();
        updateResult.Should().NotBeNull();
        updateResult!.Success.Should().BeTrue();
        queryResult2.Should().NotBeNull();
    }

    [Fact]
    public async Task InvalidToolParameters_ReturnsError()
    {
        // Arrange
        await AuthenticateAsync();
        var arguments = new Dictionary<string, object>
        {
            // Missing required 'database' parameter
            ["query"] = "SELECT * FROM Users"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/mcp/tools/query", arguments, JsonOptions);

        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}

// Result DTOs for testing
public class QueryToolResult
{
    public string[] Columns { get; set; } = Array.Empty<string>();
    public object[][] Rows { get; set; } = Array.Empty<object[]>();
    public int RowCount { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public class CommandToolResult
{
    public int AffectedRows { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
}

public class SchemaToolResult
{
    public Dictionary<string, object> Tables { get; set; } = new();
    public Dictionary<string, object> Views { get; set; } = new();
    public Dictionary<string, object> Procedures { get; set; } = new();
    public Dictionary<string, object> Functions { get; set; } = new();
}