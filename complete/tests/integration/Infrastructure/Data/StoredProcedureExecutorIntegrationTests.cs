using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using DatabaseAutomationPlatform.Infrastructure.Configuration;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Data.Parameters;
using DatabaseAutomationPlatform.Infrastructure.Logging;

namespace DatabaseAutomationPlatform.Infrastructure.Tests.Integration.Data
{
    /// <summary>
    /// Integration tests for StoredProcedureExecutor using real database connection
    /// Requires a test database with specific stored procedures
    /// </summary>
    [Collection("DatabaseIntegration")]
    public class StoredProcedureExecutorIntegrationTests : IClassFixture<DatabaseTestFixture>, IAsyncLifetime
    {
        private readonly DatabaseTestFixture _fixture;
        private readonly IStoredProcedureExecutor _executor;
        private readonly ILogger<StoredProcedureExecutorIntegrationTests> _logger;
        
        public StoredProcedureExecutorIntegrationTests(DatabaseTestFixture fixture)
        {
            _fixture = fixture;
            _logger = _fixture.ServiceProvider.GetRequiredService<ILogger<StoredProcedureExecutorIntegrationTests>>();
            _executor = _fixture.ServiceProvider.GetRequiredService<IStoredProcedureExecutor>();
        }
        public async Task InitializeAsync()
        {
            // Create test stored procedures
            await CreateTestStoredProcedures();
            
            // Insert test data
            await InsertTestData();
        }

        public async Task DisposeAsync()
        {
            // Clean up test data
            await CleanupTestData();
        }

        #region Test Setup Methods

        private async Task CreateTestStoredProcedures()
        {
            var createProceduresSql = @"
-- Test NonQuery procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestNonQuery')
    DROP PROCEDURE sp_TestNonQuery
GO
CREATE PROCEDURE sp_TestNonQuery
    @Name NVARCHAR(100),
    @Value INT,
    @ResultId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    INSERT INTO TestTable (Name, Value) VALUES (@Name, @Value)
    SET @ResultId = SCOPE_IDENTITY()
    
    RETURN @@ROWCOUNT
END
GO
-- Test Scalar procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestScalar')
    DROP PROCEDURE sp_TestScalar
GO
CREATE PROCEDURE sp_TestScalar
    @Id INT
AS
BEGIN
    SET NOCOUNT ON
    SELECT Name FROM TestTable WHERE Id = @Id
END
GO

-- Test Reader procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestReader')
    DROP PROCEDURE sp_TestReader
GO
CREATE PROCEDURE sp_TestReader
    @MinValue INT = 0
AS
BEGIN
    SET NOCOUNT ON
    SELECT Id, Name, Value, CreatedDate 
    FROM TestTable 
    WHERE Value >= @MinValue
    ORDER BY Id
END
GO

-- Test DataSet procedure (multiple result sets)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestDataSet')
    DROP PROCEDURE sp_TestDataSet
GO
CREATE PROCEDURE sp_TestDataSetAS
BEGIN
    SET NOCOUNT ON
    
    -- First result set: All records
    SELECT Id, Name, Value FROM TestTable
    
    -- Second result set: Summary
    SELECT COUNT(*) as TotalCount, SUM(Value) as TotalValue FROM TestTable
    
    -- Third result set: Top records
    SELECT TOP 5 * FROM TestTable ORDER BY Value DESC
END
GO

-- Test Transaction procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestTransaction')
    DROP PROCEDURE sp_TestTransaction
GO
CREATE PROCEDURE sp_TestTransaction
    @Name NVARCHAR(100),
    @ShouldFail BIT = 0
AS
BEGIN
    SET NOCOUNT ON
    
    INSERT INTO TestTable (Name, Value) VALUES (@Name, 100)
    
    IF @ShouldFail = 1
        RAISERROR('Simulated transaction failure', 16, 1)
        
    RETURN 0
END
GO
-- Test Timeout procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_TestTimeout')
    DROP PROCEDURE sp_TestTimeout
GO
CREATE PROCEDURE sp_TestTimeout
    @DelaySeconds INT = 5
AS
BEGIN
    SET NOCOUNT ON
    WAITFOR DELAY '00:00:05'
    SELECT 'Completed' as Result
END
GO";

            await _fixture.ExecuteSqlAsync(createProceduresSql);
            _logger.LogInformation("Test stored procedures created successfully");
        }

        private async Task InsertTestData()
        {
            var createTableSql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TestTable')
BEGIN
    CREATE TABLE TestTable (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Value INT NOT NULL,
        CreatedDate DATETIME2 DEFAULT GETUTCDATE()
    )
END";

            await _fixture.ExecuteSqlAsync(createTableSql);
            
            // Insert test data
            var insertDataSql = @"TRUNCATE TABLE TestTable

INSERT INTO TestTable (Name, Value) VALUES 
('Test Item 1', 10),
('Test Item 2', 20),
('Test Item 3', 30),
('Test Item 4', 40),
('Test Item 5', 50)";

            await _fixture.ExecuteSqlAsync(insertDataSql);
            _logger.LogInformation("Test data inserted successfully");
        }

        private async Task CleanupTestData()
        {
            var cleanupSql = @"
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TestTable')
    TRUNCATE TABLE TestTable";

            await _fixture.ExecuteSqlAsync(cleanupSql);
            _logger.LogInformation("Test data cleaned up successfully");
        }

        #endregion

        #region ExecuteNonQueryAsync Integration Tests

        [Fact]
        public async Task ExecuteNonQueryAsync_RealDatabase_InsertsDataSuccessfully()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Name", "Integration Test Item", SqlDbType.NVarChar, 100),
                StoredProcedureParameter.Input("@Value", 999, SqlDbType.Int),
                StoredProcedureParameter.Output("@ResultId", SqlDbType.Int)
            };
            // Act
            var result = await _executor.ExecuteNonQueryAsync("sp_TestNonQuery", parameters);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.RowsAffected.Should().Be(1);
            result.ReturnValue.Should().Be(1);
            result.OutputParameters.Should().ContainKey("@ResultId");
            
            var insertedId = Convert.ToInt32(result.OutputParameters["@ResultId"]);
            insertedId.Should().BeGreaterThan(0);
            
            // Verify data was actually inserted
            var verifyResult = await _executor.ExecuteScalarAsync<string>(
                "sp_TestScalar",
                new[] { StoredProcedureParameter.Input("@Id", insertedId, SqlDbType.Int) });
            
            verifyResult.Data.Should().Be("Integration Test Item");
        }

        #endregion

        #region ExecuteScalarAsync Integration Tests

        [Fact]
        public async Task ExecuteScalarAsync_RealDatabase_ReturnsCorrectValue()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@Id", 1, SqlDbType.Int)
            };
            
            // Act            var result = await _executor.ExecuteScalarAsync<string>("sp_TestScalar", parameters);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be("Test Item 1");
            result.ExecutionTimeMs.Should().BeGreaterThan(0);
        }

        #endregion

        #region ExecuteReaderAsync Integration Tests

        [Fact]
        public async Task ExecuteReaderAsync_RealDatabase_MapsDataCorrectly()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@MinValue", 25, SqlDbType.Int)
            };
            
            // Mapper function
            Func<IDataReader, TestRecord> mapper = reader => new TestRecord
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Value = reader.GetInt32(2),
                CreatedDate = reader.GetDateTime(3)
            };
            
            // Act
            var result = await _executor.ExecuteReaderAsync("sp_TestReader", mapper, parameters);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            var records = result.Data.ToList();            records.Should().HaveCount(3); // Items 3, 4, 5 have values >= 25
            records.Should().BeInAscendingOrder(r => r.Id);
            records.All(r => r.Value >= 25).Should().BeTrue();
        }

        #endregion

        #region ExecuteDataSetAsync Integration Tests

        [Fact]
        public async Task ExecuteDataSetAsync_RealDatabase_ReturnsMultipleResultSets()
        {
            // Act
            var result = await _executor.ExecuteDataSetAsync("sp_TestDataSet");
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Tables.Should().HaveCount(3);
            
            // Verify first result set (all records)
            var allRecordsTable = result.Data.Tables[0];
            allRecordsTable.Rows.Count.Should().BeGreaterThanOrEqualTo(5);
            
            // Verify second result set (summary)
            var summaryTable = result.Data.Tables[1];
            summaryTable.Rows.Should().HaveCount(1);
            summaryTable.Rows[0]["TotalCount"].Should().NotBeNull();
            
            // Verify third result set (top records)
            var topRecordsTable = result.Data.Tables[2];
            topRecordsTable.Rows.Count.Should().BeLessOrEqualTo(5);
        }

        #endregion
        #region ExecuteInTransactionAsync Integration Tests

        [Fact]
        public async Task ExecuteInTransactionAsync_Success_CommitsData()
        {
            // Arrange
            var transactionName = $"Transaction Test {Guid.NewGuid()}";
            
            Func<IDbTransaction, Task<int>> executeAction = async (transaction) =>
            {
                var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "sp_TestTransaction";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Name", transactionName));
                command.Parameters.Add(new SqlParameter("@ShouldFail", false));
                
                return await command.ExecuteNonQueryAsync();
            };
            
            // Act
            var result = await _executor.ExecuteInTransactionAsync("sp_TestTransaction", executeAction);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            
            // Verify data was committed
            var verifyCommand = _fixture.CreateCommand();
            verifyCommand.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Name = @Name";
            verifyCommand.Parameters.Add(new SqlParameter("@Name", transactionName));
            
            var count = (int)await verifyCommand.ExecuteScalarAsync();
            count.Should().Be(1);
        }
        [Fact]
        public async Task ExecuteInTransactionAsync_Failure_RollsBackData()
        {
            // Arrange
            var transactionName = $"Rollback Test {Guid.NewGuid()}";
            
            Func<IDbTransaction, Task<int>> executeAction = async (transaction) =>
            {
                var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "sp_TestTransaction";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Name", transactionName));
                command.Parameters.Add(new SqlParameter("@ShouldFail", true)); // Will cause failure
                
                return await command.ExecuteNonQueryAsync();
            };
            
            // Act
            var result = await _executor.ExecuteInTransactionAsync("sp_TestTransaction", executeAction);
            
            // Assert
            result.IsSuccess.Should().BeFalse();
            
            // Verify data was NOT committed
            var verifyCommand = _fixture.CreateCommand();
            verifyCommand.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Name = @Name";
            verifyCommand.Parameters.Add(new SqlParameter("@Name", transactionName));
            
            var count = (int)await verifyCommand.ExecuteScalarAsync();
            count.Should().Be(0);
        }
        #endregion

        #region Timeout and Performance Tests

        [Fact]
        public async Task ExecuteScalarAsync_Timeout_HandlesTimeoutGracefully()
        {
            // Arrange
            var parameters = new List<StoredProcedureParameter>
            {
                StoredProcedureParameter.Input("@DelaySeconds", 5, SqlDbType.Int)
            };
            
            // Act - Use 1 second timeout (procedure waits 5 seconds)
            var result = await _executor.ExecuteScalarAsync<string>(
                "sp_TestTimeout", 
                parameters, 
                timeoutSeconds: 1);
            
            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("operation timed out");
        }

        #endregion

        #region Test Models

        private class TestRecord
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        #endregion
    }
}