namespace DatabaseAutomationPlatform.Application.Interfaces;

/// <summary>
/// Service interface for data analytics operations
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Profiles data in a table
    /// </summary>
    Task<DataProfileResult> ProfileDataAsync(string database, string tableName, ProfileOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects data patterns
    /// </summary>
    Task<PatternDetectionResult> DetectPatternsAsync(string database, string tableName, string columnName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes data quality
    /// </summary>
    Task<DataQualityResult> AnalyzeDataQualityAsync(string database, string tableName, QualityRules rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates data insights
    /// </summary>
    Task<DataInsightsResult> GenerateInsightsAsync(string database, string query, InsightOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects anomalies in data
    /// </summary>
    Task<AnomalyDetectionResult> DetectAnomaliesAsync(string database, string tableName, AnomalyDetectionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates data visualization recommendations
    /// </summary>
    Task<VisualizationRecommendation> RecommendVisualizationsAsync(string database, string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data profile result
/// </summary>
public class DataProfileResult
{
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public Dictionary<string, ColumnProfile> Columns { get; set; } = new();
    public TableMetrics Metrics { get; set; } = new();
    public DateTimeOffset ProfiledAt { get; set; }
}

/// <summary>
/// Column profile information
/// </summary>
public class ColumnProfile
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public long DistinctCount { get; set; }
    public long NullCount { get; set; }
    public double NullPercentage { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public object? MeanValue { get; set; }
    public object? MedianValue { get; set; }
    public object? ModeValue { get; set; }
    public double? StandardDeviation { get; set; }
    public Dictionary<string, long> TopValues { get; set; } = new();
    public DataDistribution? Distribution { get; set; }
}

/// <summary>
/// Data distribution information
/// </summary>
public class DataDistribution
{
    public string Type { get; set; } = string.Empty; // Normal, Skewed, Uniform, etc.
    public double Skewness { get; set; }
    public double Kurtosis { get; set; }
    public Dictionary<string, long> Histogram { get; set; } = new();
}

/// <summary>
/// Table metrics
/// </summary>
public class TableMetrics
{
    public long DataSizeBytes { get; set; }
    public long IndexSizeBytes { get; set; }
    public double AverageRowSizeBytes { get; set; }
    public int ColumnCount { get; set; }
    public int IndexCount { get; set; }
    public double FragmentationPercentage { get; set; }
}

/// <summary>
/// Pattern detection result
/// </summary>
public class PatternDetectionResult
{
    public string ColumnName { get; set; } = string.Empty;
    public Pattern[] DetectedPatterns { get; set; } = Array.Empty<Pattern>();
    public DataTypeRecommendation[] TypeRecommendations { get; set; } = Array.Empty<DataTypeRecommendation>();
}

/// <summary>
/// Detected pattern
/// </summary>
public class Pattern
{
    public string Name { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public double MatchPercentage { get; set; }
    public string[] Examples { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Data type recommendation
/// </summary>
public class DataTypeRecommendation
{
    public string CurrentType { get; set; } = string.Empty;
    public string RecommendedType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public long? SpaceSavingsBytes { get; set; }
}

/// <summary>
/// Data quality result
/// </summary>
public class DataQualityResult
{
    public double OverallQualityScore { get; set; }
    public QualityIssue[] Issues { get; set; } = Array.Empty<QualityIssue>();
    public Dictionary<string, QualityMetrics> ColumnMetrics { get; set; } = new();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Quality issue
/// </summary>
public class QualityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long AffectedRows { get; set; }
    public string SampleQuery { get; set; } = string.Empty;
}

/// <summary>
/// Quality metrics
/// </summary>
public class QualityMetrics
{
    public double CompletenessScore { get; set; }
    public double ConsistencyScore { get; set; }
    public double ValidityScore { get; set; }
    public double UniquenessScore { get; set; }
    public double AccuracyScore { get; set; }
}

/// <summary>
/// Data insights result
/// </summary>
public class DataInsightsResult
{
    public Insight[] Insights { get; set; } = Array.Empty<Insight>();
    public Correlation[] Correlations { get; set; } = Array.Empty<Correlation>();
    public Trend[] Trends { get; set; } = Array.Empty<Trend>();
    public Outlier[] Outliers { get; set; } = Array.Empty<Outlier>();
}

/// <summary>
/// Data insight
/// </summary>
public class Insight
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double SignificanceScore { get; set; }
    public Dictionary<string, object> Supporting
Data { get; set; } = new();
    public string VisualizationType { get; set; } = string.Empty;
}

/// <summary>
/// Correlation information
/// </summary>
public class Correlation
{
    public string Column1 { get; set; } = string.Empty;
    public string Column2 { get; set; } = string.Empty;
    public double CorrelationCoefficient { get; set; }
    public string CorrelationType { get; set; } = string.Empty; // Positive, Negative, None
    public double PValue { get; set; }
}

/// <summary>
/// Trend information
/// </summary>
public class Trend
{
    public string Column { get; set; } = string.Empty;
    public string TrendType { get; set; } = string.Empty; // Increasing, Decreasing, Seasonal, etc.
    public double Slope { get; set; }
    public double R2Score { get; set; }
    public string TimeGranularity { get; set; } = string.Empty;
    public Dictionary<string, double> ForecastValues { get; set; } = new();
}

/// <summary>
/// Outlier information
/// </summary>
public class Outlier
{
    public string Column { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
    public double ZScore { get; set; }
    public string Method { get; set; } = string.Empty; // IQR, Z-Score, Isolation Forest, etc.
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Anomaly detection result
/// </summary>
public class AnomalyDetectionResult
{
    public Anomaly[] Anomalies { get; set; } = Array.Empty<Anomaly>();
    public AnomalyStatistics Statistics { get; set; } = new();
    public string[] AffectedColumns { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Anomaly information
/// </summary>
public class Anomaly
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double AnomalyScore { get; set; }
    public DateTimeOffset? DetectedAt { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Anomaly statistics
/// </summary>
public class AnomalyStatistics
{
    public int TotalAnomalies { get; set; }
    public Dictionary<string, int> AnomaliesByType { get; set; } = new();
    public double AnomalyRate { get; set; }
    public DateTimeOffset? FirstAnomaly { get; set; }
    public DateTimeOffset? LastAnomaly { get; set; }
}

/// <summary>
/// Visualization recommendation
/// </summary>
public class VisualizationRecommendation
{
    public ChartRecommendation[] Recommendations { get; set; } = Array.Empty<ChartRecommendation>();
    public DataCharacteristics Characteristics { get; set; } = new();
}

/// <summary>
/// Chart recommendation
/// </summary>
public class ChartRecommendation
{
    public string ChartType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double SuitabilityScore { get; set; }
    public ChartConfiguration Configuration { get; set; } = new();
}

/// <summary>
/// Chart configuration
/// </summary>
public class ChartConfiguration
{
    public string XAxis { get; set; } = string.Empty;
    public string YAxis { get; set; } = string.Empty;
    public string? GroupBy { get; set; }
    public string? ColorBy { get; set; }
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Data characteristics
/// </summary>
public class DataCharacteristics
{
    public string DataType { get; set; } = string.Empty; // Temporal, Categorical, Numerical, etc.
    public int DimensionCount { get; set; }
    public int MeasureCount { get; set; }
    public bool HasTimeSeries { get; set; }
    public bool HasCategories { get; set; }
    public bool HasGeospatial { get; set; }
}

// Options classes
public class ProfileOptions
{
    public bool IncludeDistribution { get; set; } = true;
    public bool IncludeTopValues { get; set; } = true;
    public int TopValuesCount { get; set; } = 10;
    public bool CalculateAdvancedStats { get; set; } = true;
    public string[]? SpecificColumns { get; set; }
}

public class QualityRules
{
    public bool CheckCompleteness { get; set; } = true;
    public bool CheckConsistency { get; set; } = true;
    public bool CheckValidity { get; set; } = true;
    public bool CheckUniqueness { get; set; } = true;
    public bool CheckAccuracy { get; set; } = true;
    public Dictionary<string, string[]> CustomRules { get; set; } = new();
}

public class InsightOptions
{
    public bool IncludeCorrelations { get; set; } = true;
    public bool IncludeTrends { get; set; } = true;
    public bool IncludeOutliers { get; set; } = true;
    public double CorrelationThreshold { get; set; } = 0.7;
    public double OutlierThreshold { get; set; } = 3.0;
}

public class AnomalyDetectionOptions
{
    public string Method { get; set; } = "IsolationForest";
    public double ContaminationRate { get; set; } = 0.1;
    public string[]? SpecificColumns { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}