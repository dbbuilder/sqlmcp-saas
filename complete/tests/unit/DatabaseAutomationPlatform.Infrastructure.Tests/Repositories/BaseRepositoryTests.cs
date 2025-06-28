using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Infrastructure.Repositories;
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

namespace DatabaseAutomationPlatform.Infrastructure.Tests.Repositories;

public class BaseRepositoryTests
{
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<ILogger<TestRepository>> _loggerMock;
    private readonly TestRepository _sut;

    public BaseRepositoryTests()
    {
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _loggerMock = new Mock<ILogger<TestRepository>>();
        _sut = new TestRepository(_executorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_EntityExists_ReturnsEntity()
    {
        // Arrange
        var expectedEntity = new TestEntity { Id = 1, Name = "Test Entity", CreatedAt = DateTimeOffset.UtcNow };
        var dataTable = CreateDataTable(expectedEntity);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_GetById",
                It.Is<SqlParameter[]>(p => p.Length == 1 && p[0].ParameterName == "@Id" && (int)p[0].Value == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expectedEntity.Id);
        result.Name.Should().Be(expectedEntity.Name);
    }

    [Fact]
    public async Task GetByIdAsync_EntityNotFound_ReturnsNull()
    {
        // Arrange
        var emptyDataTable = new DataTable();
        
        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_GetById",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyDataTable);

        // Act
        var result = await _sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity 1", CreatedAt = DateTimeOffset.UtcNow },
            new TestEntity { Id = 2, Name = "Entity 2", CreatedAt = DateTimeOffset.UtcNow }
        };
        var dataTable = CreateDataTable(entities.ToArray());

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_GetAll",
                It.Is<SqlParameter[]>(p => p.Length == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Id == 1 && e.Name == "Entity 1");
        result.Should().Contain(e => e.Id == 2 && e.Name == "Entity 2");
    }

    [Fact]
    public async Task AddAsync_ValidEntity_ReturnsAddedEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "New Entity" };
        var addedEntity = new TestEntity { Id = 1, Name = "New Entity", CreatedAt = DateTimeOffset.UtcNow };
        var dataTable = CreateDataTable(addedEntity);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_Insert",
                It.Is<SqlParameter[]>(p => 
                    p.Length == 2 &&
                    p[0].ParameterName == "@Name" && (string)p[0].Value == "New Entity" &&
                    p[1].ParameterName == "@CreatedAt"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.AddAsync(entity);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("New Entity");
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_ValidEntity_ReturnsUpdatedEntity()
    {
        // Arrange
        var entity = new TestEntity { Id = 1, Name = "Updated Entity", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var updatedEntity = new TestEntity { Id = 1, Name = "Updated Entity", CreatedAt = entity.CreatedAt, UpdatedAt = DateTimeOffset.UtcNow };
        var dataTable = CreateDataTable(updatedEntity);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_Update",
                It.Is<SqlParameter[]>(p => 
                    p.Length >= 3 &&
                    p[0].ParameterName == "@Id" && (int)p[0].Value == 1 &&
                    p[1].ParameterName == "@Name" && (string)p[1].Value == "Updated Entity"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.UpdateAsync(entity);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Updated Entity");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_EntityExists_ReturnsTrue()
    {
        // Arrange
        var outputParam = new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output, Value = true };

        _executorMock.Setup(x => x.ExecuteNonQueryAsync(
                "sp_TestEntity_Delete",
                It.Is<SqlParameter[]>(p => p.Length == 2 && p[0].ParameterName == "@Id" && (int)p[0].Value == 1),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                // Set the output parameter value
                parameters[1].Value = true;
            })
            .ReturnsAsync(1);

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_EntityNotFound_ReturnsFalse()
    {
        // Arrange
        _executorMock.Setup(x => x.ExecuteNonQueryAsync(
                "sp_TestEntity_Delete",
                It.Is<SqlParameter[]>(p => p.Length == 2 && p[0].ParameterName == "@Id"),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                // Set the output parameter value
                parameters[1].Value = false;
            })
            .ReturnsAsync(0);

        // Act
        var result = await _sut.DeleteAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_EntityExists_ReturnsTrue()
    {
        // Arrange
        var scalarResult = true;

        _executorMock.Setup(x => x.ExecuteScalarAsync<bool>(
                "sp_TestEntity_Exists",
                It.Is<SqlParameter[]>(p => p.Length == 1 && p[0].ParameterName == "@Id" && (int)p[0].Value == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(scalarResult);

        // Act
        var result = await _sut.ExistsAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new TestEntity { Id = 1, Name = "Entity 1" },
            new TestEntity { Id = 2, Name = "Entity 2" }
        };
        var dataTable = CreateDataTable(entities.ToArray());
        var totalCount = 10;

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_TestEntity_GetPaged",
                It.Is<SqlParameter[]>(p => 
                    p.Length == 3 &&
                    p[0].ParameterName == "@PageNumber" && (int)p[0].Value == 1 &&
                    p[1].ParameterName == "@PageSize" && (int)p[1].Value == 2),
                It.IsAny<CancellationToken>()))
            .Callback<string, SqlParameter[], CancellationToken>((sp, parameters, ct) =>
            {
                // Set the output parameter value
                parameters[2].Value = totalCount;
            })
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GetPagedAsync(1, 2);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(10);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.TotalPages.Should().Be(5);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
    }

    private DataTable CreateDataTable(params TestEntity[] entities)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(int));
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        dataTable.Columns.Add("UpdatedAt", typeof(DateTimeOffset));

        foreach (var entity in entities)
        {
            dataTable.Rows.Add(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt ?? DBNull.Value);
        }

        return dataTable;
    }
}

// Test entity for testing base repository
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

// Test repository implementation
public class TestRepository : BaseRepository<TestEntity, int>
{
    public TestRepository(IStoredProcedureExecutor executor, ILogger<TestRepository> logger) 
        : base(executor, logger, "TestEntity")
    {
    }

    protected override TestEntity MapFromDataRow(DataRow row)
    {
        return new TestEntity
        {
            Id = row.Field<int>("Id"),
            Name = row.Field<string>("Name") ?? string.Empty,
            CreatedAt = row.Field<DateTimeOffset>("CreatedAt"),
            UpdatedAt = row.Field<DateTimeOffset?>("UpdatedAt")
        };
    }

    protected override SqlParameter[] GetInsertParameters(TestEntity entity)
    {
        return new[]
        {
            new SqlParameter("@Name", entity.Name),
            new SqlParameter("@CreatedAt", DateTimeOffset.UtcNow)
        };
    }

    protected override SqlParameter[] GetUpdateParameters(TestEntity entity)
    {
        return new[]
        {
            new SqlParameter("@Id", entity.Id),
            new SqlParameter("@Name", entity.Name),
            new SqlParameter("@UpdatedAt", DateTimeOffset.UtcNow)
        };
    }
}