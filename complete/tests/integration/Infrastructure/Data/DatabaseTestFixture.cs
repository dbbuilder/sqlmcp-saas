using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Logging;

namespace DatabaseAutomationPlatform.Infrastructure.Tests.Integration.Data
{
    /// <summary>
    /// Test fixture for database integration tests
    /// Sets up DI container and provides database utilities
    /// </summary>
    public class DatabaseTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; }
        public IConfiguration Configuration { get; }
        private readonly string _connectionString;
        
        public DatabaseTestFixture()
        {
            // Build configuration
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
                
            _connectionString = Configuration.GetConnectionString("TestDatabase") 
                ?? "Server=localhost;Database=TestDB;Trusted_Connection=true;TrustServerCertificate=true;";
            
            // Setup DI container
            var services = new ServiceCollection();
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            // Add memory cache
            services.AddMemoryCache();
            
            // Configure options
            services.Configure<DatabaseOptions>(options =>
            {
                options.DefaultConnectionString = _connectionString;
                options.CommandTimeout = 30;
                options.EnableSqlLogging = true;
            });
            
            services.Configure<LoggingOptions>(options =>
            {
                options.ApplicationName = "DatabaseAutomationPlatform.Tests";
                options.EnvironmentName = "Test";
            });
            
            // Register services
            services.AddSingleton<ISecurityLogger, SecurityLogger>();
            services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<IStoredProcedureExecutor, StoredProcedureExecutor>();
            
            ServiceProvider = services.BuildServiceProvider();
        }
        
        public async Task ExecuteSqlAsync(string sql)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            // Split by GO statements for batch execution
            var batches = sql.Split(new[] { "GO\r\n", "GO\n", "GO\r", "\nGO" }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;
                    
                using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 60;
                
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException ex)
                {
                    // Log but continue with other batches
                    var logger = ServiceProvider.GetRequiredService<ILogger<DatabaseTestFixture>>();
                    logger.LogError(ex, "Error executing SQL batch: {Batch}", batch);
                }
            }
        }
        
        public IDbCommand CreateCommand()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection.CreateCommand();
        }
        
        public void Dispose()
        {
            (ServiceProvider as IDisposable)?.Dispose();
        }
    }
}