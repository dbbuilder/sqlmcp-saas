using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using DatabaseAutomationPlatform.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DatabaseAutomationPlatform.Infrastructure.Tests;

public class UnitOfWorkTests : IDisposable
{
    private readonly Mock<IDatabaseConnectionFactory> _connectionFactoryMock;
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<ILogger<UnitOfWork>> _loggerMock;
    private readonly Mock<IDbConnection> _connectionMock;
    private readonly Mock<IDbTransaction> _transactionMock;
    private readonly UnitOfWork _sut;

    public UnitOfWorkTests()
    {
        _connectionFactoryMock = new Mock<IDatabaseConnectionFactory>();
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _loggerMock = new Mock<ILogger<UnitOfWork>>();
        _connectionMock = new Mock<IDbConnection>();
        _transactionMock = new Mock<IDbTransaction>();

        _connectionMock.Setup(x => x.State).Returns(ConnectionState.Open);
        _connectionMock.Setup(x => x.BeginTransaction()).Returns(_transactionMock.Object);
        _connectionFactoryMock.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connectionMock.Object);

        _sut = new UnitOfWork(_connectionFactoryMock.Object, _executorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Repositories_ShouldBeInitialized()
    {
        // Assert
        _sut.AuditEvents.Should().NotBeNull();
        _sut.DatabaseTasks.Should().NotBeNull();
        _sut.DataClassifications.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldStartTransaction()
    {
        // Act
        await _sut.BeginTransactionAsync();

        // Assert
        _sut.HasActiveTransaction.Should().BeTrue();
        _connectionMock.Verify(x => x.BeginTransaction(), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenTransactionExists_ShouldThrowException()
    {
        // Arrange
        await _sut.BeginTransactionAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.BeginTransactionAsync());
    }

    [Fact]
    public async Task CommitAsync_WithActiveTransaction_ShouldCommit()
    {
        // Arrange
        await _sut.BeginTransactionAsync();

        // Act
        await _sut.CommitAsync();

        // Assert
        _sut.HasActiveTransaction.Should().BeFalse();
        _transactionMock.Verify(x => x.Commit(), Times.Once);
        _transactionMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_WithoutActiveTransaction_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CommitAsync());
    }

    [Fact]
    public async Task RollbackAsync_WithActiveTransaction_ShouldRollback()
    {
        // Arrange
        await _sut.BeginTransactionAsync();

        // Act
        await _sut.RollbackAsync();

        // Assert
        _sut.HasActiveTransaction.Should().BeFalse();
        _transactionMock.Verify(x => x.Rollback(), Times.Once);
        _transactionMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task RollbackAsync_WithoutActiveTransaction_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RollbackAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnZero()
    {
        // Act
        var result = await _sut.SaveChangesAsync();

        // Assert
        result.Should().Be(0); // Since we're using stored procedures, SaveChanges is a no-op
    }

    [Fact]
    public async Task Dispose_WithActiveTransaction_ShouldRollbackAndDispose()
    {
        // Arrange
        await _sut.BeginTransactionAsync();

        // Act
        _sut.Dispose();

        // Assert
        _transactionMock.Verify(x => x.Rollback(), Times.Once);
        _transactionMock.Verify(x => x.Dispose(), Times.Once);
        _connectionMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_WithoutActiveTransaction_ShouldOnlyDisposeConnection()
    {
        // Act
        _sut.Dispose();

        // Assert
        _transactionMock.Verify(x => x.Rollback(), Times.Never);
        _connectionMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task TransactionScenario_CompleteWorkflow_ShouldWork()
    {
        // Arrange
        var auditEvent = new AuditEvent
        {
            UserId = "test-user",
            Action = "TestAction",
            EntityType = "TestEntity",
            EntityId = "123",
            Success = true
        };

        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(long));
        dataTable.Columns.Add("EventTime", typeof(DateTimeOffset));
        dataTable.Columns.Add("UserId", typeof(string));
        dataTable.Columns.Add("UserName", typeof(string));
        dataTable.Columns.Add("Action", typeof(string));
        dataTable.Columns.Add("EntityType", typeof(string));
        dataTable.Columns.Add("EntityId", typeof(string));
        dataTable.Columns.Add("Success", typeof(bool));
        dataTable.Columns.Add("Severity", typeof(string));

        dataTable.Rows.Add(1L, DateTimeOffset.UtcNow, auditEvent.UserId, "Test User", 
            auditEvent.Action, auditEvent.EntityType, auditEvent.EntityId, 
            auditEvent.Success, "Information");

        _executorMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        await _sut.BeginTransactionAsync();
        var result = await _sut.AuditEvents.AddAsync(auditEvent);
        await _sut.CommitAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1L);
        _transactionMock.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public async Task TransactionScenario_WithError_ShouldRollback()
    {
        // Arrange
        _executorMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqlException("Test error"));

        // Act
        await _sut.BeginTransactionAsync();
        
        var act = async () => await _sut.AuditEvents.GetAllAsync();
        
        await act.Should().ThrowAsync<SqlException>();
        await _sut.RollbackAsync();

        // Assert
        _transactionMock.Verify(x => x.Rollback(), Times.Once);
        _sut.HasActiveTransaction.Should().BeFalse();
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }
}