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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DatabaseAutomationPlatform.Application.Tests.Services;

public class AnalyticsServiceTests
{
    private readonly Mock<IStoredProcedureExecutor> _executorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AnalyticsService>> _loggerMock;
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        _executorMock = new Mock<IStoredProcedureExecutor>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AnalyticsService>>();
        _sut = new AnalyticsService(_executorMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ProfileDataAsync_ReturnsCompleteDataProfile()
    {
        // Arrange
        var database = "TestDB";
        var tableName = "Users";
        var options = new ProfileOptions
        {
            IncludeDistribution = true,
            IncludeTopValues = true,
            TopValuesCount = 5,
            CalculateAdvancedStats = true
        };

        // Mock table metrics
        var metricsTable = new DataTable();
        metricsTable.Columns.Add("RowCount", typeof(long));
        metricsTable.Columns.Add("DataSizeBytes", typeof(long));
        metricsTable.Columns.Add("IndexSizeBytes", typeof(long));
        metricsTable.Columns.Add("ColumnCount", typeof(int));
        metricsTable.Columns.Add("IndexCount", typeof(int));
        metricsTable.Columns.Add("FragmentationPercentage", typeof(double));
        metricsTable.Rows.Add(1000000L, 104857600L, 20971520L, 10, 3, 5.2);

        // Mock column profiles
        var profileTable = new DataTable();
        profileTable.Columns.Add("ColumnName", typeof(string));
        profileTable.Columns.Add("DataType", typeof(string));
        profileTable.Columns.Add("DistinctCount", typeof(long));
        profileTable.Columns.Add("NullCount", typeof(long));
        profileTable.Columns.Add("MinValue", typeof(string));
        profileTable.Columns.Add("MaxValue", typeof(string));
        profileTable.Columns.Add("MeanValue", typeof(double));
        profileTable.Columns.Add("MedianValue", typeof(double));
        profileTable.Columns.Add("ModeValue", typeof(string));
        profileTable.Columns.Add("StandardDeviation", typeof(double));
        
        profileTable.Rows.Add("Age", "int", 80L, 1000L, "18", "95", 35.5, 34.0, "30", 12.3);
        profileTable.Rows.Add("Email", "nvarchar", 999000L, 0L, "a@example.com", "z@example.com", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

        // Mock top values
        var topValuesTable = new DataTable();
        topValuesTable.Columns.Add("ColumnName", typeof(string));
        topValuesTable.Columns.Add("Value", typeof(string));
        topValuesTable.Columns.Add("Count", typeof(long));
        
        topValuesTable.Rows.Add("Age", "30", 25000L);
        topValuesTable.Rows.Add("Age", "35", 22000L);
        topValuesTable.Rows.Add("Age", "28", 20000L);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metricsTable)
            .ReturnsAsync(profileTable)
            .ReturnsAsync(topValuesTable);

        // Act
        var result = await _sut.ProfileDataAsync(database, tableName, options);

        // Assert
        result.Should().NotBeNull();
        result.TableName.Should().Be(tableName);
        result.RowCount.Should().Be(1000000);
        result.Columns.Should().HaveCount(2);
        result.Columns["Age"].DistinctCount.Should().Be(80);
        result.Columns["Age"].NullPercentage.Should().Be(0.1);
        result.Columns["Age"].MeanValue.Should().Be(35.5);
        result.Columns["Email"].NullCount.Should().Be(0);
        result.Metrics.DataSizeBytes.Should().Be(104857600);
    }

    [Fact]
    public async Task DetectPatternsAsync_IdentifiesCommonPatterns()
    {
        // Arrange
        var database = "TestDB";
        var tableName = "Contacts";
        var columnName = "Phone";

        var patternsTable = new DataTable();
        patternsTable.Columns.Add("PatternName", typeof(string));
        patternsTable.Columns.Add("RegexPattern", typeof(string));
        patternsTable.Columns.Add("MatchPercentage", typeof(double));
        patternsTable.Columns.Add("Examples", typeof(string));
        patternsTable.Columns.Add("Description", typeof(string));
        
        patternsTable.Rows.Add("US Phone", @"^\(\d{3}\) \d{3}-\d{4}$", 85.5, "(555) 123-4567;(555) 987-6543", "US phone number format");
        patternsTable.Rows.Add("International", @"^\+\d{1,3} \d+$", 10.2, "+1 5551234567;+44 2012345678", "International phone format");

        var recommendationsTable = new DataTable();
        recommendationsTable.Columns.Add("CurrentType", typeof(string));
        recommendationsTable.Columns.Add("RecommendedType", typeof(string));
        recommendationsTable.Columns.Add("Reason", typeof(string));
        recommendationsTable.Columns.Add("ConfidenceScore", typeof(double));
        recommendationsTable.Columns.Add("SpaceSavingsBytes", typeof(long));
        
        recommendationsTable.Rows.Add("nvarchar(50)", "varchar(20)", "No unicode characters detected", 0.95, 1500000L);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patternsTable)
            .ReturnsAsync(recommendationsTable);

        // Act
        var result = await _sut.DetectPatternsAsync(database, tableName, columnName);

        // Assert
        result.Should().NotBeNull();
        result.ColumnName.Should().Be(columnName);
        result.DetectedPatterns.Should().HaveCount(2);
        result.DetectedPatterns[0].Name.Should().Be("US Phone");
        result.DetectedPatterns[0].MatchPercentage.Should().Be(85.5);
        result.TypeRecommendations.Should().HaveCount(1);
        result.TypeRecommendations[0].SpaceSavingsBytes.Should().Be(1500000);
    }

    [Fact]
    public async Task AnalyzeDataQualityAsync_IdentifiesQualityIssues()
    {
        // Arrange
        var database = "TestDB";
        var tableName = "Products";
        var rules = new QualityRules
        {
            CheckCompleteness = true,
            CheckConsistency = true,
            CheckValidity = true,
            CheckUniqueness = true,
            CheckAccuracy = true
        };

        var qualityTable = new DataTable();
        qualityTable.Columns.Add("IssueType", typeof(string));
        qualityTable.Columns.Add("Severity", typeof(string));
        qualityTable.Columns.Add("Column", typeof(string));
        qualityTable.Columns.Add("Description", typeof(string));
        qualityTable.Columns.Add("AffectedRows", typeof(long));
        qualityTable.Columns.Add("SampleQuery", typeof(string));
        
        qualityTable.Rows.Add("Missing Values", "High", "Description", "85% of rows have NULL descriptions", 850000L, 
            "SELECT * FROM Products WHERE Description IS NULL");
        qualityTable.Rows.Add("Duplicate Values", "Medium", "SKU", "Found 1,250 duplicate SKUs", 2500L, 
            "SELECT SKU, COUNT(*) FROM Products GROUP BY SKU HAVING COUNT(*) > 1");
        qualityTable.Rows.Add("Invalid Format", "Low", "Price", "12 rows have negative prices", 12L, 
            "SELECT * FROM Products WHERE Price < 0");

        var metricsTable = new DataTable();
        metricsTable.Columns.Add("Column", typeof(string));
        metricsTable.Columns.Add("CompletenessScore", typeof(double));
        metricsTable.Columns.Add("ConsistencyScore", typeof(double));
        metricsTable.Columns.Add("ValidityScore", typeof(double));
        metricsTable.Columns.Add("UniquenessScore", typeof(double));
        metricsTable.Columns.Add("AccuracyScore", typeof(double));
        
        metricsTable.Rows.Add("Description", 0.15, 0.90, 0.95, 1.0, 0.85);
        metricsTable.Rows.Add("SKU", 1.0, 0.85, 1.0, 0.995, 0.98);
        metricsTable.Rows.Add("Price", 1.0, 0.95, 0.999, 1.0, 0.99);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityTable)
            .ReturnsAsync(metricsTable);

        // Act
        var result = await _sut.AnalyzeDataQualityAsync(database, tableName, rules);

        // Assert
        result.Should().NotBeNull();
        result.Issues.Should().HaveCount(3);
        result.Issues.Should().Contain(i => i.Type == "Missing Values" && i.Severity == "High");
        result.ColumnMetrics.Should().HaveCount(3);
        result.ColumnMetrics["Description"].CompletenessScore.Should().Be(0.15);
        result.OverallQualityScore.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
        result.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateInsightsAsync_ReturnsDataInsights()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT * FROM Sales WHERE Date >= '2024-01-01'";
        var options = new InsightOptions
        {
            IncludeCorrelations = true,
            IncludeTrends = true,
            IncludeOutliers = true,
            CorrelationThreshold = 0.7,
            OutlierThreshold = 3.0
        };

        var insightsTable = new DataTable();
        insightsTable.Columns.Add("Type", typeof(string));
        insightsTable.Columns.Add("Title", typeof(string));
        insightsTable.Columns.Add("Description", typeof(string));
        insightsTable.Columns.Add("SignificanceScore", typeof(double));
        insightsTable.Columns.Add("VisualizationType", typeof(string));
        
        insightsTable.Rows.Add("Revenue Growth", "Strong Q1 Revenue Growth", 
            "Revenue increased by 25% compared to previous quarter", 0.92, "LineChart");
        insightsTable.Rows.Add("Seasonality", "Weekly Sales Pattern", 
            "Sales peak on Fridays and Saturdays", 0.85, "BarChart");

        var correlationsTable = new DataTable();
        correlationsTable.Columns.Add("Column1", typeof(string));
        correlationsTable.Columns.Add("Column2", typeof(string));
        correlationsTable.Columns.Add("CorrelationCoefficient", typeof(double));
        correlationsTable.Columns.Add("CorrelationType", typeof(string));
        correlationsTable.Columns.Add("PValue", typeof(double));
        
        correlationsTable.Rows.Add("Temperature", "IceCreamSales", 0.89, "Positive", 0.001);
        correlationsTable.Rows.Add("Price", "Quantity", -0.75, "Negative", 0.003);

        var trendsTable = new DataTable();
        trendsTable.Columns.Add("Column", typeof(string));
        trendsTable.Columns.Add("TrendType", typeof(string));
        trendsTable.Columns.Add("Slope", typeof(double));
        trendsTable.Columns.Add("R2Score", typeof(double));
        trendsTable.Columns.Add("TimeGranularity", typeof(string));
        
        trendsTable.Rows.Add("Revenue", "Increasing", 1250.5, 0.87, "Daily");

        var outliersTable = new DataTable();
        outliersTable.Columns.Add("Column", typeof(string));
        outliersTable.Columns.Add("Value", typeof(string));
        outliersTable.Columns.Add("ZScore", typeof(double));
        outliersTable.Columns.Add("Method", typeof(string));
        outliersTable.Columns.Add("Query", typeof(string));
        
        outliersTable.Rows.Add("OrderAmount", "125000", 4.2, "Z-Score", 
            "SELECT * FROM Orders WHERE OrderAmount = 125000");

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(insightsTable)
            .ReturnsAsync(correlationsTable)
            .ReturnsAsync(trendsTable)
            .ReturnsAsync(outliersTable);

        // Act
        var result = await _sut.GenerateInsightsAsync(database, query, options);

        // Assert
        result.Should().NotBeNull();
        result.Insights.Should().HaveCount(2);
        result.Insights[0].SignificanceScore.Should().Be(0.92);
        result.Correlations.Should().HaveCount(2);
        result.Correlations[0].CorrelationCoefficient.Should().Be(0.89);
        result.Trends.Should().HaveCount(1);
        result.Trends[0].Slope.Should().Be(1250.5);
        result.Outliers.Should().HaveCount(1);
        result.Outliers[0].ZScore.Should().Be(4.2);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_FindsDataAnomalies()
    {
        // Arrange
        var database = "TestDB";
        var tableName = "Transactions";
        var options = new AnomalyDetectionOptions
        {
            Method = "IsolationForest",
            ContaminationRate = 0.05,
            SpecificColumns = new[] { "Amount", "Duration" }
        };

        var anomaliesTable = new DataTable();
        anomaliesTable.Columns.Add("Type", typeof(string));
        anomaliesTable.Columns.Add("Description", typeof(string));
        anomaliesTable.Columns.Add("AnomalyScore", typeof(double));
        anomaliesTable.Columns.Add("DetectedAt", typeof(DateTime));
        anomaliesTable.Columns.Add("Query", typeof(string));
        
        var now = DateTime.UtcNow;
        anomaliesTable.Rows.Add("Unusual Pattern", "Abnormally high transaction amount", 0.95, now,
            "SELECT * FROM Transactions WHERE Id = 12345");
        anomaliesTable.Rows.Add("Time Anomaly", "Transaction outside business hours", 0.88, now.AddHours(-2),
            "SELECT * FROM Transactions WHERE Id = 12346");

        var statsTable = new DataTable();
        statsTable.Columns.Add("TotalAnomalies", typeof(int));
        statsTable.Columns.Add("AnomalyRate", typeof(double));
        statsTable.Columns.Add("FirstAnomaly", typeof(DateTime));
        statsTable.Columns.Add("LastAnomaly", typeof(DateTime));
        
        statsTable.Rows.Add(125, 0.025, now.AddDays(-7), now);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(anomaliesTable)
            .ReturnsAsync(statsTable);

        // Act
        var result = await _sut.DetectAnomaliesAsync(database, tableName, options);

        // Assert
        result.Should().NotBeNull();
        result.Anomalies.Should().HaveCount(2);
        result.Anomalies[0].AnomalyScore.Should().Be(0.95);
        result.Statistics.TotalAnomalies.Should().Be(125);
        result.Statistics.AnomalyRate.Should().Be(0.025);
        result.AffectedColumns.Should().Contain("Amount", "Duration");
    }

    [Fact]
    public async Task RecommendVisualizationsAsync_SuggestsAppropriateCharts()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT Category, SUM(Sales) as TotalSales, COUNT(*) as Count FROM Orders GROUP BY Category";

        var characteristicsTable = new DataTable();
        characteristicsTable.Columns.Add("DataType", typeof(string));
        characteristicsTable.Columns.Add("DimensionCount", typeof(int));
        characteristicsTable.Columns.Add("MeasureCount", typeof(int));
        characteristicsTable.Columns.Add("HasTimeSeries", typeof(bool));
        characteristicsTable.Columns.Add("HasCategories", typeof(bool));
        characteristicsTable.Columns.Add("HasGeospatial", typeof(bool));
        
        characteristicsTable.Rows.Add("Categorical", 1, 2, false, true, false);

        var recommendationsTable = new DataTable();
        recommendationsTable.Columns.Add("ChartType", typeof(string));
        recommendationsTable.Columns.Add("Reason", typeof(string));
        recommendationsTable.Columns.Add("SuitabilityScore", typeof(double));
        recommendationsTable.Columns.Add("XAxis", typeof(string));
        recommendationsTable.Columns.Add("YAxis", typeof(string));
        recommendationsTable.Columns.Add("GroupBy", typeof(string));
        
        recommendationsTable.Rows.Add("BarChart", "Best for comparing categories", 0.95, "Category", "TotalSales", null);
        recommendationsTable.Rows.Add("PieChart", "Good for showing proportions", 0.80, "Category", "TotalSales", null);
        recommendationsTable.Rows.Add("TreeMap", "Effective for hierarchical data", 0.75, "Category", "TotalSales", null);

        _executorMock.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(characteristicsTable)
            .ReturnsAsync(recommendationsTable);

        // Act
        var result = await _sut.RecommendVisualizationsAsync(database, query);

        // Assert
        result.Should().NotBeNull();
        result.Recommendations.Should().HaveCount(3);
        result.Recommendations[0].ChartType.Should().Be("BarChart");
        result.Recommendations[0].SuitabilityScore.Should().Be(0.95);
        result.Characteristics.DataType.Should().Be("Categorical");
        result.Characteristics.HasCategories.Should().BeTrue();
        result.Characteristics.DimensionCount.Should().Be(1);
        result.Characteristics.MeasureCount.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ProfileDataAsync_InvalidDatabase_ThrowsArgumentException(string database)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _sut.ProfileDataAsync(database, "TestTable", new ProfileOptions()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ProfileDataAsync_InvalidTableName_ThrowsArgumentException(string tableName)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _sut.ProfileDataAsync("TestDB", tableName, new ProfileOptions()));
    }

    [Fact]
    public async Task GenerateInsightsAsync_WithAuditLog_CreatesAuditEvent()
    {
        // Arrange
        var database = "TestDB";
        var query = "SELECT * FROM Sales";
        var options = new InsightOptions();
        
        // Mock minimal response
        var insightsTable = new DataTable();
        insightsTable.Columns.Add("Type", typeof(string));
        insightsTable.Columns.Add("Title", typeof(string));
        insightsTable.Columns.Add("Description", typeof(string));
        insightsTable.Columns.Add("SignificanceScore", typeof(double));
        insightsTable.Columns.Add("VisualizationType", typeof(string));
        
        _executorMock.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SqlParameter[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(insightsTable);

        var auditRepoMock = new Mock<IRepository<AuditEvent, Guid>>();
        _unitOfWorkMock.Setup(x => x.AuditEvents).Returns(auditRepoMock.Object);

        // Act
        await _sut.GenerateInsightsAsync(database, query, options);

        // Assert
        _unitOfWorkMock.Verify(x => x.AuditEvents.AddAsync(
            It.Is<AuditEvent>(a => 
                a.Action == "GenerateInsights" &&
                a.EntityType == "Analytics" &&
                a.Success == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}