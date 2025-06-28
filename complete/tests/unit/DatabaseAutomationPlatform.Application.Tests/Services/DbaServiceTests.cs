using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Application.Services;
using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DatabaseAutomationPlatform.Application.Tests.Services;

public class DbaServiceTests
{
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DbaService>> _loggerMock;
    private readonly DbaService _sut;

    public DbaServiceTests()
    {
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DbaService>>();
        _sut = new DbaService(_executorMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetDatabaseHealthAsync_ReturnsHealthMetrics()
    {
        // Arrange
        var database = "TestDB";
        var dataTable = new DataTable();
        dataTable.Columns.Add("MetricName", typeof(string));
        dataTable.Columns.Add("MetricValue", typeof(string));
        dataTable.Columns.Add("Unit", typeof(string));
        dataTable.Columns.Add("Status", typeof(string));
        dataTable.Columns.Add("Threshold", typeof(decimal));
        
        dataTable.Rows.Add("CPU_Usage", "45", "Percent", "Normal", 80);
        dataTable.Rows.Add("Memory_Usage", "60", "Percent", "Warning", 75);
        dataTable.Rows.Add("Disk_Space", "85", "Percent", "Critical", 80);
        dataTable.Rows.Add("Active_Connections", "150", "Count", "Normal", 500);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GetDatabaseHealth",
                It.Is<SqlParameter[]>(p => 
                    p[0].ParameterName == "@Database" && (string)p[0].Value == database),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GetDatabaseHealthAsync(database);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.Should().HaveCount(4);
        result.Metrics["CPU_Usage"].Value.Should().Be("45");
        result.Metrics["CPU_Usage"].Status.Should().Be("Normal");
        result.Metrics["Memory_Usage"].Status.Should().Be("Warning");
        result.Metrics["Disk_Space"].Status.Should().Be("Critical");
        result.CriticalIssues.Should().ContainSingle(i => i.Contains("Disk_Space"));
        result.Warnings.Should().ContainSingle(w => w.Contains("Memory_Usage"));
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_ReturnsPerformanceAnalysis()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT * FROM LargeTable WHERE Status = 'Active'";
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("QueryHash", typeof(string));
        dataTable.Columns.Add("ExecutionTimeMs", typeof(long));
        dataTable.Columns.Add("CpuTime", typeof(decimal));
        dataTable.Columns.Add("LogicalReads", typeof(decimal));
        dataTable.Columns.Add("PhysicalReads", typeof(decimal));
        dataTable.Columns.Add("ExecutionPlan", typeof(string));
        dataTable.Columns.Add("Bottleneck", typeof(string));
        dataTable.Columns.Add("Recommendation", typeof(string));
        
        dataTable.Rows.Add("ABC123", 1500L, 1200m, 50000m, 100m, "<ShowPlanXML>...</ShowPlanXML>", 
            "Table Scan", "Add index on Status column");
        dataTable.Rows.Add("ABC123", 1500L, 1200m, 50000m, 100m, "<ShowPlanXML>...</ShowPlanXML>", 
            "High Logical Reads", "Consider query optimization");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_AnalyzePerformance",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.AnalyzePerformanceAsync(database, query);

        // Assert
        result.Should().NotBeNull();
        result.QueryHash.Should().Be("ABC123");
        result.ExecutionTimeMs.Should().Be(1500);
        result.CpuTime.Should().Be(1200);
        result.LogicalReads.Should().Be(50000);
        result.Bottlenecks.Should().HaveCount(2);
        result.Recommendations.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetIndexRecommendationsAsync_ReturnsRecommendations()
    {
        // Arrange
        var database = "TestDB";
        var tableName = "Users";
        
        var recommendationsTable = new DataTable();
        recommendationsTable.Columns.Add("IndexName", typeof(string));
        recommendationsTable.Columns.Add("Columns", typeof(string));
        recommendationsTable.Columns.Add("IncludedColumns", typeof(string));
        recommendationsTable.Columns.Add("Reason", typeof(string));
        recommendationsTable.Columns.Add("EstimatedImprovement", typeof(decimal));
        recommendationsTable.Columns.Add("CreateStatement", typeof(string));
        
        recommendationsTable.Rows.Add("IX_Users_Status", "Status", "Name,Email", 
            "Missing index for frequent query filter", 0.75m,
            "CREATE INDEX IX_Users_Status ON Users(Status) INCLUDE (Name, Email)");

        var currentIndexesTable = new DataTable();
        currentIndexesTable.Columns.Add("IndexName", typeof(string));
        currentIndexesTable.Columns.Add("UserSeeks", typeof(long));
        currentIndexesTable.Columns.Add("UserScans", typeof(long));
        currentIndexesTable.Columns.Add("UserLookups", typeof(long));
        currentIndexesTable.Columns.Add("UserUpdates", typeof(long));
        
        currentIndexesTable.Rows.Add("PK_Users", 1000000L, 500L, 0L, 50000L);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendationsTable)
            .ReturnsAsync(currentIndexesTable);

        // Act
        var result = await _sut.GetIndexRecommendationsAsync(database, tableName);

        // Assert
        result.Should().NotBeNull();
        result.Recommendations.Should().HaveCount(1);
        result.Recommendations[0].IndexName.Should().Be("IX_Users_Status");
        result.Recommendations[0].EstimatedImprovement.Should().Be(0.75);
        result.CurrentIndexes.Should().HaveCount(1);
        result.CurrentIndexes[0].UserSeeks.Should().Be(1000000);
    }

    [Fact]
    public async Task BackupDatabaseAsync_Success_ReturnsBackupResult()
    {
        // Arrange
        var database = "TestDB";
        var backupPath = @"C:\Backups\TestDB_Full.bak";
        var backupType = BackupType.Full;
        
        _executorMock.Setup(x => x.ExecuteNonQueryAsync(
                "sp_BackupDatabase",
                It.Is<SqlParameter[]>(p => 
                    p[0].ParameterName == "@Database" &&
                    p[1].ParameterName == "@BackupPath" &&
                    p[2].ParameterName == "@BackupType"),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                // Set output parameters
                parameters[3].Value = true; // Success
                parameters[4].Value = 1073741824L; // 1GB
                parameters[5].Value = 120000L; // 2 minutes
            })
            .ReturnsAsync(1);

        // Act
        var result = await _sut.BackupDatabaseAsync(database, backupPath, backupType);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.BackupPath.Should().Be(backupPath);
        result.BackupSizeBytes.Should().Be(1073741824);
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(120000));

        // Verify audit
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.Is<AuditEvent>(a => 
                a.Action == "BackupDatabase" &&
                a.Success == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCompleteStatistics()
    {
        // Arrange
        var database = "TestDB";
        
        var statsTable = new DataTable();
        statsTable.Columns.Add("SizeMB", typeof(long));
        statsTable.Columns.Add("DataSizeMB", typeof(long));
        statsTable.Columns.Add("IndexSizeMB", typeof(long));
        statsTable.Columns.Add("TableCount", typeof(int));
        statsTable.Columns.Add("IndexCount", typeof(int));
        statsTable.Columns.Add("ProcedureCount", typeof(int));
        statsTable.Columns.Add("ViewCount", typeof(int));
        statsTable.Columns.Add("UserCount", typeof(int));
        
        statsTable.Rows.Add(5120L, 3072L, 2048L, 50, 125, 75, 20, 15);

        var tableStatsTable = new DataTable();
        tableStatsTable.Columns.Add("TableName", typeof(string));
        tableStatsTable.Columns.Add("RowCount", typeof(long));
        tableStatsTable.Columns.Add("DataSizeKB", typeof(long));
        tableStatsTable.Columns.Add("IndexSizeKB", typeof(long));
        
        tableStatsTable.Rows.Add("Users", 1000000L, 512000L, 256000L);
        tableStatsTable.Rows.Add("Orders", 5000000L, 2048000L, 1024000L);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(statsTable)
            .ReturnsAsync(tableStatsTable);

        // Act
        var result = await _sut.GetStatisticsAsync(database);

        // Assert
        result.Should().NotBeNull();
        result.SizeMB.Should().Be(5120);
        result.DataSizeMB.Should().Be(3072);
        result.IndexSizeMB.Should().Be(2048);
        result.TableCount.Should().Be(50);
        result.Tables.Should().HaveCount(2);
        result.Tables["Users"].RowCount.Should().Be(1000000);
    }

    [Fact]
    public async Task RunMaintenanceAsync_Success_ReturnsMaintenanceResult()
    {
        // Arrange
        var database = "TestDB";
        var maintenanceType = MaintenanceType.IndexRebuild;
        
        var resultsTable = new DataTable();
        resultsTable.Columns.Add("TaskName", typeof(string));
        resultsTable.Columns.Add("Success", typeof(bool));
        resultsTable.Columns.Add("Message", typeof(string));
        
        resultsTable.Rows.Add("Rebuild IX_Users_Status", true, "Index rebuilt successfully");
        resultsTable.Rows.Add("Rebuild IX_Orders_Date", true, "Index rebuilt successfully");
        resultsTable.Rows.Add("Rebuild IX_Products_Category", false, "Lock timeout exceeded");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_RunMaintenance",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultsTable);

        // Act
        var result = await _sut.RunMaintenanceAsync(database, maintenanceType);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Overall success despite one failure
        result.Type.Should().Be(maintenanceType);
        result.TasksCompleted.Should().HaveCount(2);
        result.TasksFailed.Should().HaveCount(1);
        result.TasksFailed[0].Should().Contain("IX_Products_Category");
    }
}