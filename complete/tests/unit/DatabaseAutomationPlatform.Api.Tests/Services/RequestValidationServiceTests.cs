using DatabaseAutomationPlatform.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DatabaseAutomationPlatform.Api.Tests.Services;

public class RequestValidationServiceTests
{
    private readonly Mock<ILogger<RequestValidationService>> _loggerMock;
    private readonly RequestValidationService _sut;

    public RequestValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<RequestValidationService>>();
        _sut = new RequestValidationService(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateRequestAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 1
        };

        // Act
        var result = await _sut.ValidateRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateRequestAsync_NullRequest_ReturnsFailure()
    {
        // Act
        var result = await _sut.ValidateRequestAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Be("Request cannot be null");
    }

    [Fact]
    public async Task ValidateRequestAsync_InvalidJsonRpcVersion_ReturnsFailure()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "1.0", // Invalid version
            Method = "tools/list",
            Id = 1
        };

        // Act
        var result = await _sut.ValidateRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "jsonrpc");
    }

    [Fact]
    public async Task ValidateRequestAsync_MissingMethod_ReturnsFailure()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "", // Empty method
            Id = 1
        };

        // Act
        var result = await _sut.ValidateRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "method");
    }

    [Fact]
    public async Task ValidateRequestAsync_MissingId_ReturnsFailure()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = null // Missing ID
        };

        // Act
        var result = await _sut.ValidateRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "id");
    }

    [Theory]
    [InlineData("query")]
    [InlineData("execute")]
    [InlineData("schema")]
    [InlineData("analyze")]
    public async Task ValidateToolParametersAsync_ValidToolName_ChecksRequiredParameters(string toolName)
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.ValidateToolParametersAsync(toolName, parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Field == "database");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_QueryTool_ValidParameters_ReturnsSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT * FROM Users",
            ["timeout"] = 30
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("query", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateToolParametersAsync_QueryTool_InvalidTimeout_ReturnsFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT * FROM Users",
            ["timeout"] = 500 // Too high
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("query", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "timeout");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_UnknownTool_ReturnsFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.ValidateToolParametersAsync("unknown", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("Unknown tool"));
    }

    [Fact]
    public async Task ValidateSqlQueryAsync_ValidQuery_ReturnsSuccess()
    {
        // Arrange
        var query = "SELECT Id, Name FROM Users WHERE Active = 1";

        // Act
        var result = await _sut.ValidateSqlQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateSqlQueryAsync_EmptyQuery_ReturnsFailure()
    {
        // Arrange
        var query = "";

        // Act
        var result = await _sut.ValidateSqlQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message == "Query cannot be empty");
    }

    [Theory]
    [InlineData("SELECT * FROM Users; DROP TABLE Users")]
    [InlineData("SELECT * FROM Users WHERE Name = 'test' OR 1=1")]
    [InlineData("SELECT * FROM Users -- malicious comment")]
    [InlineData("SELECT * FROM Users; EXEC xp_cmdshell 'dir'")]
    public async Task ValidateSqlQueryAsync_SqlInjectionPatterns_ReturnsFailure(string query)
    {
        // Act
        var result = await _sut.ValidateSqlQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "SQL_INJECTION_RISK");
    }

    [Theory]
    [InlineData("DROP TABLE Users")]
    [InlineData("DELETE FROM Users")]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("ALTER TABLE Users ADD COLUMN")]
    public async Task ValidateSqlQueryAsync_DangerousOperations_ReturnsFailure(string query)
    {
        // Act
        var result = await _sut.ValidateSqlQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DANGEROUS_OPERATION");
    }

    [Fact]
    public async Task ValidateSqlQueryAsync_QueryTooLong_ReturnsFailure()
    {
        // Arrange
        var query = new string('a', 10001);

        // Act
        var result = await _sut.ValidateSqlQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "QUERY_TOO_LONG");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_SchemaToolWithValidObjectType_ReturnsSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["objectType"] = "table",
            ["objectName"] = "Users"
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("schema", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateToolParametersAsync_SchemaToolWithInvalidObjectType_ReturnsFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["objectType"] = "invalid",
            ["objectName"] = "Users"
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("schema", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "objectType");
    }

    [Fact]
    public async Task ValidateToolParametersAsync_AnalyzeToolWithValidAnalysisType_ReturnsSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["analysisType"] = "performance",
            ["target"] = "SELECT * FROM Users"
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("analyze", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateToolParametersAsync_AnalyzeToolWithInvalidAnalysisType_ReturnsFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["analysisType"] = "invalid",
            ["target"] = "SELECT * FROM Users"
        };

        // Act
        var result = await _sut.ValidateToolParametersAsync("analyze", parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "analysisType");
    }
}