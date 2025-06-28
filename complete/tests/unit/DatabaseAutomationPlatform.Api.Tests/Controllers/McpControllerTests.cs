using DatabaseAutomationPlatform.Api.Controllers;
using DatabaseAutomationPlatform.Api.Models;
using DatabaseAutomationPlatform.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace DatabaseAutomationPlatform.Api.Tests.Controllers;

public class McpControllerTests
{
    private readonly Mock<IMcpService> _mcpServiceMock;
    private readonly Mock<IRequestValidationService> _validationServiceMock;
    private readonly Mock<ILogger<McpController>> _loggerMock;
    private readonly McpController _sut;

    public McpControllerTests()
    {
        _mcpServiceMock = new Mock<IMcpService>();
        _validationServiceMock = new Mock<IRequestValidationService>();
        _loggerMock = new Mock<ILogger<McpController>>();
        
        _sut = new McpController(_mcpServiceMock.Object, _validationServiceMock.Object, _loggerMock.Object);
        
        // Setup default HTTP context
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("role", "Developer")
        }, "test"));
        
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ProcessRequest_ValidRequest_ReturnsOkWithResponse()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 1
        };

        var validationResult = ValidationResult.Success();
        _validationServiceMock.Setup(x => x.ValidateRequestAsync(It.IsAny<McpRequest>()))
            .ReturnsAsync(validationResult);

        var mcpResponse = new McpResponse
        {
            Id = 1,
            Result = new[] { "tool1", "tool2" }
        };
        _mcpServiceMock.Setup(x => x.ProcessRequestAsync(It.IsAny<McpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResponse);

        // Act
        var result = await _sut.ProcessRequest(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(mcpResponse);
    }

    [Fact]
    public async Task ProcessRequest_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "1.0", // Invalid version
            Method = "tools/list",
            Id = 1
        };

        var validationResult = ValidationResult.Failure(new[]
        {
            new ValidationError { Field = "jsonrpc", Message = "Invalid JSON-RPC version" }
        });
        _validationServiceMock.Setup(x => x.ValidateRequestAsync(It.IsAny<McpRequest>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _sut.ProcessRequest(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Status.Should().Be(400);
        errorResponse.Errors.Should().ContainKey("jsonrpc");
    }

    [Fact]
    public async Task GetCapabilities_ReturnsOkWithCapabilities()
    {
        // Arrange
        var capabilities = new McpCapabilities
        {
            Tools = new[] { "query", "execute" },
            Resources = new[] { "databases", "tables" },
            Prompts = new[] { "sql-generation" }
        };
        _mcpServiceMock.Setup(x => x.GetCapabilitiesAsync())
            .ReturnsAsync(capabilities);

        // Act
        var result = await _sut.GetCapabilities();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(capabilities);
    }

    [Fact]
    public async Task ListTools_ReturnsOkWithTools()
    {
        // Arrange
        var tools = new List<McpTool>
        {
            new McpTool { Name = "query", Description = "Execute query" },
            new McpTool { Name = "execute", Description = "Execute command" }
        };
        _mcpServiceMock.Setup(x => x.ListToolsAsync())
            .ReturnsAsync(tools);

        // Act
        var result = await _sut.ListTools();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(tools);
    }

    [Fact]
    public async Task ExecuteTool_ValidParameters_ReturnsOkWithResult()
    {
        // Arrange
        var toolName = "query";
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT * FROM Users"
        };

        var validationResult = ValidationResult.Success();
        _validationServiceMock.Setup(x => x.ValidateToolParametersAsync(toolName, parameters))
            .ReturnsAsync(validationResult);

        var toolResult = new McpToolResult
        {
            Content = new { data = "result" }
        };
        _mcpServiceMock.Setup(x => x.ExecuteToolAsync(toolName, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _sut.ExecuteTool(toolName, parameters);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(toolResult);
    }

    [Fact]
    public async Task ExecuteTool_InvalidParameters_ReturnsBadRequest()
    {
        // Arrange
        var toolName = "query";
        var parameters = new Dictionary<string, object>
        {
            // Missing required 'database' parameter
            ["query"] = "SELECT * FROM Users"
        };

        var validationResult = ValidationResult.Failure(new[]
        {
            new ValidationError { Field = "database", Message = "Database is required" }
        });
        _validationServiceMock.Setup(x => x.ValidateToolParametersAsync(toolName, parameters))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _sut.ExecuteTool(toolName, parameters);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var errorResponse = badRequestResult!.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Status.Should().Be(400);
        errorResponse.Errors.Should().ContainKey("database");
    }

    [Fact]
    public async Task ExecuteTool_ExecutionError_ReturnsInternalServerError()
    {
        // Arrange
        var toolName = "query";
        var parameters = new Dictionary<string, object>
        {
            ["database"] = "TestDB",
            ["query"] = "SELECT * FROM Users"
        };

        var validationResult = ValidationResult.Success();
        _validationServiceMock.Setup(x => x.ValidateToolParametersAsync(toolName, parameters))
            .ReturnsAsync(validationResult);

        var toolResult = new McpToolResult
        {
            IsError = true,
            ErrorMessage = "Database connection failed"
        };
        _mcpServiceMock.Setup(x => x.ExecuteToolAsync(toolName, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _sut.ExecuteTool(toolName, parameters);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        var errorResponse = objectResult.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Detail.Should().Be("Database connection failed");
    }

    [Fact]
    public async Task ProcessRequest_UsesCorrelationIdFromContext()
    {
        // Arrange
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/list",
            Id = 1
        };

        var validationResult = ValidationResult.Success();
        _validationServiceMock.Setup(x => x.ValidateRequestAsync(It.IsAny<McpRequest>()))
            .ReturnsAsync(validationResult);

        var mcpResponse = new McpResponse { Id = 1 };
        _mcpServiceMock.Setup(x => x.ProcessRequestAsync(It.IsAny<McpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mcpResponse);

        // Act
        await _sut.ProcessRequest(request);

        // Assert
        _loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("test-correlation-id")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteTool_EmptyToolName_ReturnsBadRequest()
    {
        // Arrange
        var toolName = "";
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.ExecuteTool(toolName, parameters);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}