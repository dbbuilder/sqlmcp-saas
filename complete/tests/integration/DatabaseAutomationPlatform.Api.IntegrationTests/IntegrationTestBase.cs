using DatabaseAutomationPlatform.Api.Services;
using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DatabaseAutomationPlatform.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<IntegrationTestBase.CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = Factory.CreateClient();
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected async Task AuthenticateAsync()
    {
        // For testing, we'll create a mock JWT token
        // In a real scenario, this would call the auth endpoint
        Client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "test-integration-token");
    }

    protected async Task<McpResponse?> SendMcpRequestAsync(McpRequest request)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/mcp", request, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<McpResponse>(JsonOptions);
        }
        return null;
    }

    protected async Task<T?> ExecuteToolAsync<T>(string toolName, Dictionary<string, object> arguments) where T : class
    {
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "tools/call",
            Id = Random.Shared.Next(1, 1000),
            Params = new Dictionary<string, object>
            {
                ["name"] = toolName,
                ["arguments"] = arguments
            }
        };

        var response = await SendMcpRequestAsync(request);
        if (response?.Result != null)
        {
            var result = JsonSerializer.Deserialize<McpToolResult>(
                JsonSerializer.Serialize(response.Result), JsonOptions);
            
            if (result?.Content != null && !result.IsError)
            {
                return JsonSerializer.Deserialize<T>(
                    JsonSerializer.Serialize(result.Content), JsonOptions);
            }
        }
        
        return null;
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove real database services
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDatabaseConnectionFactory));
                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                var executorDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IStoredProcedureExecutor));
                if (executorDescriptor != null)
                {
                    services.Remove(executorDescriptor);
                }

                // Add mock implementations
                services.AddSingleton<IDatabaseConnectionFactory>(sp =>
                {
                    var mock = new Mock<IDatabaseConnectionFactory>();
                    return mock.Object;
                });

                services.AddScoped<IStoredProcedureExecutor>(sp =>
                {
                    var mock = new Mock<IStoredProcedureExecutor>();
                    SetupMockExecutor(mock);
                    return mock.Object;
                });

                // Override authentication for testing
                services.Configure<JwtSettings>(options =>
                {
                    options.SecretKey = "test-secret-key-for-integration-testing-only-123456";
                    options.Issuer = "test-issuer";
                    options.Audience = "test-audience";
                });

                // Add test logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            });
        }

        private static void SetupMockExecutor(Mock<IStoredProcedureExecutor> mock)
        {
            // Setup sp_ExecuteQuery
            mock.Setup(x => x.ExecuteAsync(
                    "sp_ExecuteQuery",
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string sp, SqlParameter[] parameters, CancellationToken ct) =>
                {
                    var dataTable = new DataTable();
                    dataTable.Columns.Add("Id", typeof(int));
                    dataTable.Columns.Add("Name", typeof(string));
                    dataTable.Columns.Add("Email", typeof(string));
                    
                    dataTable.Rows.Add(1, "John Doe", "john@example.com");
                    dataTable.Rows.Add(2, "Jane Smith", "jane@example.com");
                    
                    // Set output parameter
                    var execTimeParam = parameters.FirstOrDefault(p => p.ParameterName == "@ExecutionTimeMs");
                    if (execTimeParam != null)
                    {
                        execTimeParam.Value = 25L;
                    }
                    
                    return dataTable;
                });

            // Setup sp_ExecuteCommand
            mock.Setup(x => x.ExecuteNonQueryAsync(
                    "sp_ExecuteCommand",
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string sp, SqlParameter[] parameters, CancellationToken ct) =>
                {
                    // Set output parameters
                    var affectedRowsParam = parameters.FirstOrDefault(p => p.ParameterName == "@AffectedRows");
                    if (affectedRowsParam != null)
                    {
                        affectedRowsParam.Value = 1;
                    }
                    
                    var execTimeParam = parameters.FirstOrDefault(p => p.ParameterName == "@ExecutionTimeMs");
                    if (execTimeParam != null)
                    {
                        execTimeParam.Value = 15L;
                    }
                    
                    return 1;
                });

            // Setup sp_GetSchemaInfo
            mock.Setup(x => x.ExecuteAsync(
                    "sp_GetSchemaInfo",
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string sp, SqlParameter[] parameters, CancellationToken ct) =>
                {
                    var dataTable = new DataTable();
                    dataTable.Columns.Add("TableName", typeof(string));
                    dataTable.Columns.Add("Schema", typeof(string));
                    dataTable.Columns.Add("ColumnName", typeof(string));
                    dataTable.Columns.Add("DataType", typeof(string));
                    dataTable.Columns.Add("MaxLength", typeof(int));
                    dataTable.Columns.Add("IsNullable", typeof(bool));
                    dataTable.Columns.Add("IsIdentity", typeof(bool));
                    dataTable.Columns.Add("DefaultValue", typeof(string));
                    dataTable.Columns.Add("OrdinalPosition", typeof(int));
                    dataTable.Columns.Add("CreatedDate", typeof(DateTimeOffset));
                    dataTable.Columns.Add("ModifiedDate", typeof(DateTimeOffset));
                    
                    var now = DateTimeOffset.UtcNow;
                    dataTable.Rows.Add("Users", "dbo", "Id", "int", DBNull.Value, false, true, DBNull.Value, 1, now, now);
                    dataTable.Rows.Add("Users", "dbo", "Name", "nvarchar", 100, false, false, DBNull.Value, 2, now, now);
                    dataTable.Rows.Add("Users", "dbo", "Email", "nvarchar", 255, false, false, DBNull.Value, 3, now, now);
                    
                    return dataTable;
                });

            // Setup sp_GetDatabaseHealth
            mock.Setup(x => x.ExecuteAsync(
                    "sp_GetDatabaseHealth",
                    It.IsAny<SqlParameter[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string sp, SqlParameter[] parameters, CancellationToken ct) =>
                {
                    var dataTable = new DataTable();
                    dataTable.Columns.Add("MetricName", typeof(string));
                    dataTable.Columns.Add("MetricValue", typeof(string));
                    dataTable.Columns.Add("Unit", typeof(string));
                    dataTable.Columns.Add("Status", typeof(string));
                    dataTable.Columns.Add("Threshold", typeof(decimal));
                    
                    dataTable.Rows.Add("CPU_Usage", "45", "Percent", "Normal", 80);
                    dataTable.Rows.Add("Memory_Usage", "60", "Percent", "Normal", 75);
                    dataTable.Rows.Add("Disk_Space", "30", "Percent", "Normal", 80);
                    
                    return dataTable;
                });

            // Add more mock setups as needed...
        }
    }
}

// Mock implementations for missing types
public class SqlParameter
{
    public string ParameterName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public ParameterDirection Direction { get; set; }
}

public enum ParameterDirection
{
    Input,
    Output,
    InputOutput,
    ReturnValue
}