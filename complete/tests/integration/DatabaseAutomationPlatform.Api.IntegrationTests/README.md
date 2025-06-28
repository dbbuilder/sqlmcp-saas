# Database Automation Platform - Integration Tests

This project contains integration tests for the Database Automation Platform API.

## Overview

The integration tests verify that all components of the system work together correctly, including:
- API endpoints
- MCP protocol implementation
- Application services (Developer, DBA, Schema, Analytics)
- Authentication and authorization
- Error handling and validation

## Test Structure

### Test Files

1. **McpEndpointIntegrationTests.cs**
   - Tests basic MCP protocol endpoints
   - Verifies authentication requirements
   - Tests capabilities, tools listing, and basic request processing

2. **ServiceIntegrationTests.cs**
   - Tests end-to-end service integration with real database operations
   - Uses Testcontainers for SQL Server
   - Tests complex scenarios involving multiple services

3. **ToolExecutionIntegrationTests.cs**
   - Tests individual tool execution through the MCP protocol
   - Uses mocked stored procedures for faster execution
   - Focuses on service integration without database dependencies

4. **IntegrationTestBase.cs**
   - Base class providing common test infrastructure
   - Configures mock services and authentication
   - Provides helper methods for test execution

## Running the Tests

### Prerequisites

- .NET 8.0 SDK
- Docker (for ServiceIntegrationTests that use Testcontainers)
- Visual Studio 2022 or VS Code with C# extension

### Command Line

```bash
# Run all integration tests
dotnet test tests/integration/DatabaseAutomationPlatform.Api.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~McpEndpointIntegrationTests"

# Run with detailed output
dotnet test -v detailed

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Build the solution
3. Run all tests or select specific tests to run

## Test Categories

### Authentication Tests
- Verify JWT bearer token authentication
- Test API key authentication
- Verify unauthorized access is blocked

### MCP Protocol Tests
- Test initialize method
- Test tools/list endpoint
- Test tools/call with various tools
- Verify error handling for invalid requests

### Service Integration Tests
- **Developer Service**: Query execution, command execution, SQL generation
- **DBA Service**: Performance analysis, health monitoring, backup operations
- **Schema Service**: Schema information retrieval, comparison, migration
- **Analytics Service**: Data profiling, pattern detection, anomaly detection

### End-to-End Scenarios
- Create table → Insert data → Query → Update → Delete
- Cross-service operations (e.g., analyze performance after schema changes)

## Mock Configuration

The integration tests use a custom `WebApplicationFactory` that:
- Replaces real database connections with mocks
- Configures test authentication
- Sets up mock stored procedure responses
- Enables detailed logging for debugging

## Writing New Tests

1. Inherit from `IntegrationTestBase` for common functionality
2. Use `AuthenticateAsync()` to set up authentication
3. Use `ExecuteToolAsync<T>()` for easy tool execution
4. Follow the AAA pattern (Arrange, Act, Assert)

Example:
```csharp
[Fact]
public async Task MyNewTest()
{
    // Arrange
    await AuthenticateAsync();
    var arguments = new Dictionary<string, object>
    {
        ["database"] = "TestDB",
        ["query"] = "SELECT * FROM MyTable"
    };

    // Act
    var result = await ExecuteToolAsync<QueryToolResult>("query", arguments);

    // Assert
    result.Should().NotBeNull();
    result.RowCount.Should().BeGreaterThan(0);
}
```

## Troubleshooting

### Tests failing with authentication errors
- Ensure JWT settings are configured in test environment
- Check that mock authentication is properly set up

### Database connection errors
- For ServiceIntegrationTests: Ensure Docker is running
- For other tests: Verify mock services are configured

### Timeout errors
- Increase test timeout in test settings
- Check for deadlocks in async code

## CI/CD Integration

These tests should be run in the CI/CD pipeline:
```yaml
- name: Run Integration Tests
  run: dotnet test tests/integration/DatabaseAutomationPlatform.Api.IntegrationTests --logger "console;verbosity=normal"
```

## Best Practices

1. Keep tests independent - each test should set up its own data
2. Use meaningful test names that describe what is being tested
3. Clean up resources after tests (handled automatically by base class)
4. Use FluentAssertions for readable assertions
5. Mock external dependencies to keep tests fast and reliable