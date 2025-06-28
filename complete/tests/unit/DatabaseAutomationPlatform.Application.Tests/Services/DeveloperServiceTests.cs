using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Application.Services;
using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DatabaseAutomationPlatform.Application.Tests.Services;

public class DeveloperServiceTests
{
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeveloperService>> _loggerMock;
    private readonly DeveloperService _sut;

    public DeveloperServiceTests()
    {
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeveloperService>>();
        _sut = new DeveloperService(_executorMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ValidQuery_ReturnsResults()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT Id, Name FROM Users WHERE Active = 1";
        var timeout = 30;

        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Rows.Add(1, "John Doe");
        dataTable.Rows.Add(2, "Jane Smith");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_ExecuteQuery",
                It.Is<SqlParameter[]>(p => 
                    p.Length == 4 &&
                    p[0].ParameterName == "@Database" && (string)p[0].Value == database &&
                    p[1].ParameterName == "@Query" && (string)p[1].Value == query &&
                    p[2].ParameterName == "@Timeout" && (int)p[2].Value == timeout &&
                    p[3].ParameterName == "@ExecutionTimeMs" && p[3].Direction == ParameterDirection.Output),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                parameters[3].Value = 25; // Execution time
            })
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.ExecuteQueryAsync(database, query, timeout);

        // Assert
        result.Should().NotBeNull();
        result.Columns.Should().BeEquivalentTo(new[] { "Id", "Name" });
        result.Rows.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
        result.ExecutionTimeMs.Should().Be(25);

        // Verify audit was logged
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.Is<AuditEvent>(a => 
                a.Action == "ExecuteQuery" &&
                a.EntityType == "Database" &&
                a.EntityId == database &&
                a.Success == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteQueryAsync_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var database = "TestDB";
        var query = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _sut.ExecuteQueryAsync(database, query));

        // Verify no audit was logged
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteQueryAsync_QueryFails_LogsFailureAndThrows()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT * FROM NonExistentTable";
        var expectedError = "Invalid object name 'NonExistentTable'";

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_ExecuteQuery",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqlException(expectedError));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SqlException>(() => 
            _sut.ExecuteQueryAsync(database, query));

        // Verify failure audit was logged
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.Is<AuditEvent>(a => 
                a.Action == "ExecuteQuery" &&
                a.Success == false &&
                a.ErrorMessage!.Contains(expectedError)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ValidCommand_ReturnsResult()
    {
        // Arrange
        var database = "TestDB";
        var command = "UPDATE Users SET LastLogin = GETUTCDATE() WHERE Id = 1";
        var useTransaction = true;
        var affectedRows = 1;

        _executorMock.Setup(x => x.ExecuteNonQueryAsync(
                "sp_ExecuteCommand",
                It.Is<SqlParameter[]>(p => 
                    p.Length == 5 &&
                    p[0].ParameterName == "@Database" && (string)p[0].Value == database &&
                    p[1].ParameterName == "@Command" && (string)p[1].Value == command &&
                    p[2].ParameterName == "@UseTransaction" && (bool)p[2].Value == useTransaction),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                parameters[3].Value = affectedRows; // Rows affected
                parameters[4].Value = 15; // Execution time
            })
            .ReturnsAsync(affectedRows);

        // Act
        var result = await _sut.ExecuteCommandAsync(database, command, useTransaction);

        // Assert
        result.Should().NotBeNull();
        result.AffectedRows.Should().Be(1);
        result.Success.Should().BeTrue();
        result.ExecutionTimeMs.Should().Be(15);

        // Verify transaction was used
        _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithoutTransaction_DoesNotUseTransaction()
    {
        // Arrange
        var database = "TestDB";
        var command = "UPDATE Users SET LastLogin = GETUTCDATE() WHERE Id = 1";
        var useTransaction = false;

        _executorMock.Setup(x => x.ExecuteNonQueryAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _sut.ExecuteCommandAsync(database, command, useTransaction);

        // Assert
        _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSqlAsync_ValidDescription_ReturnsGeneratedSql()
    {
        // Arrange
        var database = "TestDB";
        var description = "Get all active users with their last login date";
        var context = "Users table has columns: Id, Name, Email, Active, LastLogin";

        var dataTable = new DataTable();
        dataTable.Columns.Add("GeneratedSql", typeof(string));
        dataTable.Columns.Add("Explanation", typeof(string));
        dataTable.Columns.Add("ConfidenceScore", typeof(decimal));
        dataTable.Rows.Add(
            "SELECT Id, Name, Email, LastLogin FROM Users WHERE Active = 1",
            "This query selects all columns for active users including their last login date",
            0.95m
        );

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GenerateSql",
                It.Is<SqlParameter[]>(p => 
                    p[0].ParameterName == "@Database" &&
                    p[1].ParameterName == "@Description" &&
                    p[2].ParameterName == "@Context"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GenerateSqlAsync(database, description, context);

        // Assert
        result.Should().NotBeNull();
        result.GeneratedSql.Should().Contain("SELECT");
        result.GeneratedSql.Should().Contain("Active = 1");
        result.Explanation.Should().NotBeEmpty();
        result.ConfidenceScore.Should().Be(0.95);
    }

    [Fact]
    public async Task OptimizeQueryAsync_ValidQuery_ReturnsOptimizedVersion()
    {
        // Arrange
        var database = "TestDB";
        var originalQuery = "SELECT * FROM Users WHERE Name LIKE '%John%'";

        var dataTable = new DataTable();
        dataTable.Columns.Add("OptimizedQuery", typeof(string));
        dataTable.Columns.Add("Recommendation", typeof(string));
        dataTable.Columns.Add("EstimatedImprovement", typeof(decimal));
        dataTable.Columns.Add("OriginalCost", typeof(decimal));
        dataTable.Columns.Add("OptimizedCost", typeof(decimal));
        
        dataTable.Rows.Add(
            "SELECT Id, Name, Email FROM Users WHERE Name LIKE '%John%'",
            "Avoid SELECT * and specify only needed columns",
            0.30m,
            100m,
            70m
        );

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_OptimizeQuery",
                It.Is<SqlParameter[]>(p => 
                    p[0].ParameterName == "@Database" &&
                    p[1].ParameterName == "@Query"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.OptimizeQueryAsync(database, originalQuery);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedQuery.Should().NotContain("SELECT *");
        result.Recommendations.Should().ContainSingle();
        result.EstimatedImprovement.Should().Be(0.30);
    }

    [Fact]
    public async Task ValidateSqlAsync_ValidSql_ReturnsValid()
    {
        // Arrange
        var database = "TestDB";
        var sql = "SELECT Id, Name FROM Users WHERE Active = 1";

        var dataTable = new DataTable();
        dataTable.Columns.Add("IsValid", typeof(bool));
        dataTable.Columns.Add("Error", typeof(string));
        dataTable.Columns.Add("Warning", typeof(string));
        dataTable.Columns.Add("Suggestion", typeof(string));
        
        dataTable.Rows.Add(true, DBNull.Value, DBNull.Value, "Consider adding an index on Active column");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_ValidateSql",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.ValidateSqlAsync(database, sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Suggestions.Should().ContainKey("Performance");
    }

    [Fact]
    public async Task ValidateSqlAsync_InvalidSql_ReturnsInvalid()
    {
        // Arrange
        var database = "TestDB";
        var sql = "SELCT Id FROM Users"; // Typo in SELECT

        var dataTable = new DataTable();
        dataTable.Columns.Add("IsValid", typeof(bool));
        dataTable.Columns.Add("Error", typeof(string));
        dataTable.Columns.Add("Warning", typeof(string));
        dataTable.Columns.Add("Suggestion", typeof(string));
        
        dataTable.Rows.Add(false, "Incorrect syntax near 'SELCT'", DBNull.Value, DBNull.Value);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_ValidateSql",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.ValidateSqlAsync(database, sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("SELCT"));
    }
}