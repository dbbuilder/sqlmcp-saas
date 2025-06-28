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

public class SchemaServiceTests
{
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SchemaService>> _loggerMock;
    private readonly SchemaService _sut;

    public SchemaServiceTests()
    {
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SchemaService>>();
        _sut = new SchemaService(_executorMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetSchemaInfoAsync_ReturnsCompleteSchemaInformation()
    {
        // Arrange
        var database = "TestDB";
        var objectType = ObjectType.Table;
        string? objectName = null;

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
        dataTable.Rows.Add("Orders", "dbo", "Id", "int", DBNull.Value, false, true, DBNull.Value, 1, now, now);
        dataTable.Rows.Add("Orders", "dbo", "UserId", "int", DBNull.Value, false, false, DBNull.Value, 2, now, now);
        dataTable.Rows.Add("Orders", "dbo", "OrderDate", "datetime", DBNull.Value, false, false, "GETUTCDATE()", 3, now, now);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GetSchemaInfo",
                It.Is<SqlParameter[]>(p => 
                    p[0].ParameterName == "@Database" && (string)p[0].Value == database &&
                    p[1].ParameterName == "@ObjectType" && (string)p[1].Value == objectType.ToString() &&
                    p[2].ParameterName == "@ObjectName" && p[2].Value == DBNull.Value),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GetSchemaInfoAsync(database, objectType, objectName);

        // Assert
        result.Should().NotBeNull();
        result.Tables.Should().HaveCount(2);
        result.Tables["Users"].Columns.Should().HaveLength(3);
        result.Tables["Users"].Columns[0].Name.Should().Be("Id");
        result.Tables["Users"].Columns[0].IsIdentity.Should().BeTrue();
        result.Tables["Orders"].Columns[2].DefaultValue.Should().Be("GETUTCDATE()");
    }

    [Fact]
    public async Task CompareSchemaAsync_IdentifiesDifferences()
    {
        // Arrange
        var sourceDatabase = "SourceDB";
        var targetDatabase = "TargetDB";
        var options = new ComparisonOptions
        {
            IncludeTables = true,
            IncludeViews = true,
            IncludeProcedures = true
        };
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("ObjectType", typeof(string));
        dataTable.Columns.Add("ObjectName", typeof(string));
        dataTable.Columns.Add("DifferenceType", typeof(string));
        dataTable.Columns.Add("SourceDefinition", typeof(string));
        dataTable.Columns.Add("TargetDefinition", typeof(string));
        dataTable.Columns.Add("Details", typeof(string));
        
        dataTable.Rows.Add("Table", "Products", "Added", "CREATE TABLE Products...", DBNull.Value, "Table exists in source but not in target");
        dataTable.Rows.Add("Column", "Users.Email", "Modified", "nvarchar(255) NOT NULL", "nvarchar(100) NULL", "Data type and nullability changed");
        dataTable.Rows.Add("StoredProcedure", "sp_GetOrderDetails", "Removed", DBNull.Value, "CREATE PROCEDURE sp_GetOrderDetails...", "Procedure exists in target but not in source");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_CompareSchema",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.CompareSchemaAsync(sourceDatabase, targetDatabase, options);

        // Assert
        result.Should().NotBeNull();
        result.Differences.Should().HaveCount(3);
        result.TotalDifferences.Should().Be(3);
        result.Differences.Should().Contain(d => d.ObjectName == "Products" && d.Type == DifferenceType.Added);
        result.Differences.Should().Contain(d => d.ObjectName == "Users.Email" && d.Type == DifferenceType.Modified);
        result.Differences.Should().Contain(d => d.ObjectName == "sp_GetOrderDetails" && d.Type == DifferenceType.Removed);
        result.DifferenceCounts[DifferenceType.Added].Should().Be(1);
        result.DifferenceCounts[DifferenceType.Modified].Should().Be(1);
        result.DifferenceCounts[DifferenceType.Removed].Should().Be(1);
    }

    [Fact]
    public async Task GenerateMigrationAsync_CreatesValidMigrationScript()
    {
        // Arrange
        var sourceDatabase = "SourceDB";
        var targetDatabase = "TargetDB";
        var options = new MigrationOptions
        {
            IncludeDropStatements = false,
            UseTransactions = true,
            GenerateRollback = true,
            PreserveData = true
        };
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("UpScript", typeof(string));
        dataTable.Columns.Add("Warnings", typeof(string));
        dataTable.Columns.Add("RequiresDataMigration", typeof(bool));
        dataTable.Columns.Add("EstimatedDurationSeconds", typeof(long));
        
        // First row contains the main script
        dataTable.Rows.Add(
            "BEGIN TRANSACTION;\nALTER TABLE Users ADD Email nvarchar(255);\nCOMMIT;",
            "Column addition may require application code changes",
            false,
            30L
        );
        
        // Additional rows for steps
        dataTable.Columns.Add("Order", typeof(int));
        dataTable.Columns.Add("Description", typeof(string));
        dataTable.Columns.Add("Script", typeof(string));
        dataTable.Columns.Add("IsReversible", typeof(bool));
        dataTable.Columns.Add("RollbackScript", typeof(string));
        
        dataTable.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 1, 
            "Add Email column to Users table", 
            "ALTER TABLE Users ADD Email nvarchar(255);", 
            true, 
            "ALTER TABLE Users DROP COLUMN Email;");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GenerateMigration",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GenerateMigrationAsync(sourceDatabase, targetDatabase, options);

        // Assert
        result.Should().NotBeNull();
        result.Script.Should().Contain("ALTER TABLE Users ADD Email");
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Description.Should().Be("Add Email column to Users table");
        result.Steps[0].IsReversible.Should().BeTrue();
        result.RequiresDataMigration.Should().BeFalse();
        result.EstimatedDurationSeconds.Should().Be(30);
        result.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateDocumentationAsync_CreatesMarkdownDocumentation()
    {
        // Arrange
        var database = "TestDB";
        var options = new DocumentationOptions
        {
            Format = "Markdown",
            IncludeDescriptions = true,
            IncludeExamples = true,
            IncludeDependencies = true
        };
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("Section", typeof(string));
        dataTable.Columns.Add("Content", typeof(string));
        dataTable.Columns.Add("Order", typeof(int));
        
        dataTable.Rows.Add("Overview", "# TestDB Database Documentation\n\nThis database contains...", 1);
        dataTable.Rows.Add("Tables", "## Tables\n\n### Users Table\n\nStores user information...", 2);
        dataTable.Rows.Add("Procedures", "## Stored Procedures\n\n### sp_GetUsers\n\nRetrieves user data...", 3);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GenerateDocumentation",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.GenerateDocumentationAsync(database, options);

        // Assert
        result.Should().NotBeNull();
        result.Format.Should().Be("Markdown");
        result.Content.Should().Contain("# TestDB Database Documentation");
        result.Content.Should().Contain("## Tables");
        result.Content.Should().Contain("## Stored Procedures");
        result.Sections.Should().HaveCount(3);
        result.Sections["Overview"].Should().Contain("# TestDB Database Documentation");
        result.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnalyzeDependenciesAsync_ReturnsDependencyGraph()
    {
        // Arrange
        var database = "TestDB";
        var objectName = "sp_ProcessOrder";
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("DependentObject", typeof(string));
        dataTable.Columns.Add("DependentType", typeof(string));
        dataTable.Columns.Add("ReferencedObject", typeof(string));
        dataTable.Columns.Add("ReferencedType", typeof(string));
        dataTable.Columns.Add("DependencyType", typeof(string));
        dataTable.Columns.Add("Level", typeof(int));
        
        dataTable.Rows.Add("sp_ProcessOrder", "Procedure", "Orders", "Table", "Select", 1);
        dataTable.Rows.Add("sp_ProcessOrder", "Procedure", "Users", "Table", "Select", 1);
        dataTable.Rows.Add("sp_ProcessOrder", "Procedure", "fn_CalculateTotal", "Function", "Call", 1);
        dataTable.Rows.Add("vw_OrderSummary", "View", "sp_ProcessOrder", "Procedure", "Reference", 2);

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_AnalyzeDependencies",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Mock sp_GetObjectType call
        var typeDataTable = new DataTable();
        typeDataTable.Columns.Add("ObjectType", typeof(string));
        typeDataTable.Rows.Add("Procedure");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_GetObjectType",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeDataTable);

        // Act
        var result = await _sut.AnalyzeDependenciesAsync(database, objectName);

        // Assert
        result.Should().NotBeNull();
        result.ObjectName.Should().Be("sp_ProcessOrder");
        result.ObjectType.Should().Be(ObjectType.Procedure);
        result.Dependencies.Should().HaveCount(3);
        result.Dependents.Should().HaveCount(1);
        result.Dependencies.Should().Contain(d => d.Name == "Orders" && d.Type == ObjectType.Table);
        result.Dependents.Should().Contain(d => d.Name == "vw_OrderSummary");
        result.ImpactAnalysis.Should().ContainSingle(i => i.Contains("1 dependent objects"));
    }

    [Fact]
    public async Task ValidateSchemaAsync_ReturnsValidationResults()
    {
        // Arrange
        var database = "TestDB";
        var rules = new ValidationRules
        {
            CheckNamingConventions = true,
            CheckDataTypes = true,
            CheckIndexes = true,
            CheckConstraints = true,
            CheckSecurity = true,
            CheckPerformance = true
        };
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("RuleName", typeof(string));
        dataTable.Columns.Add("ObjectName", typeof(string));
        dataTable.Columns.Add("Message", typeof(string));
        dataTable.Columns.Add("Severity", typeof(string));
        dataTable.Columns.Add("Recommendation", typeof(string));
        
        dataTable.Rows.Add("NamingConvention", "Users", "Table 'Users' should start with 'tbl_'", "Warning", "Rename table to follow naming convention");
        dataTable.Rows.Add("PrimaryKey", "Logs", "Table 'Logs' is missing a primary key", "Error", "Add a primary key to the table");
        dataTable.Rows.Add("TableSize", "AuditEvents", "Table 'AuditEvents' exceeds max size (1.5M rows)", "Warning", "Consider archiving old records");

        _executorMock.Setup(x => x.ExecuteAsync(
                "sp_ValidateSchema",
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        // Act
        var result = await _sut.ValidateSchemaAsync(database, rules);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCount(3);
        result.Issues.Should().Contain(v => v.Category == "NamingConvention" && v.Severity == IssueSeverity.Warning);
        result.Issues.Should().Contain(v => v.Category == "PrimaryKey" && v.Severity == IssueSeverity.Error);
        result.IssueCounts["NamingConvention"].Should().Be(1);
        result.IssueCounts["PrimaryKey"].Should().Be(1);
        result.IssueCounts["TableSize"].Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetSchemaInfoAsync_InvalidDatabase_ThrowsArgumentException(string database)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _sut.GetSchemaInfoAsync(database, ObjectType.Table));
    }

    [Fact]
    public async Task GenerateMigrationAsync_WithAuditLog_CreatesAuditEvent()
    {
        // Arrange
        var sourceDatabase = "SourceDB";
        var targetDatabase = "TargetDB";
        var options = new MigrationOptions();
        
        var dataTable = new DataTable();
        dataTable.Columns.Add("UpScript", typeof(string));
        dataTable.Columns.Add("Warnings", typeof(string));
        dataTable.Columns.Add("RequiresDataMigration", typeof(bool));
        dataTable.Columns.Add("EstimatedDurationSeconds", typeof(long));
        dataTable.Rows.Add("-- Migration script", "", false, 10L);

        _executorMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataTable);

        var auditRepoMock = new Mock<IRepository<AuditEvent, Guid>>();
        _unitOfWorkMock.Setup(x => x.AuditEvents).Returns(auditRepoMock.Object);

        // Act
        await _sut.GenerateMigrationAsync(sourceDatabase, targetDatabase, options);

        // Assert
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.Is<AuditEvent>(a => 
                a.Action == "GenerateMigration" &&
                a.EntityType == "Schema" &&
                a.Success == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}