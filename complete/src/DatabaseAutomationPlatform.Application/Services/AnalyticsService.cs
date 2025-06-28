using DatabaseAutomationPlatform.Application.Interfaces;
using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Application.Services;

/// <summary>
/// Service implementation for data analytics operations
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IStoredProcedureExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IStoredProcedureExecutor executor,
        IUnitOfWork unitOfWork,
        ILogger<AnalyticsService> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataProfileResult> ProfileDataAsync(string database, string tableName, ProfileOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Profiling data for table {TableName} in database {Database}", tableName, database);

        // Get table metrics
        var metricsParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName)
        };

        var metricsResult = await _executor.ExecuteAsync("sp_GetTableMetrics", metricsParams, cancellationToken);
        
        if (metricsResult.Rows.Count == 0)
        {
            throw new InvalidOperationException($"Table {tableName} not found in database {database}");
        }

        var metricsRow = metricsResult.Rows[0];
        var rowCount = metricsRow.Field<long>("RowCount");
        var metrics = new TableMetrics
        {
            DataSizeBytes = metricsRow.Field<long>("DataSizeBytes"),
            IndexSizeBytes = metricsRow.Field<long>("IndexSizeBytes"),
            AverageRowSizeBytes = rowCount > 0 ? (double)metricsRow.Field<long>("DataSizeBytes") / rowCount : 0,
            ColumnCount = metricsRow.Field<int>("ColumnCount"),
            IndexCount = metricsRow.Field<int>("IndexCount"),
            FragmentationPercentage = metricsRow.Field<double>("FragmentationPercentage")
        };

        // Get column profiles
        var profileParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@Options", JsonSerializer.Serialize(options))
        };

        var profileResult = await _executor.ExecuteAsync("sp_ProfileColumns", profileParams, cancellationToken);
        
        var columnProfiles = new Dictionary<string, ColumnProfile>();
        
        foreach (DataRow row in profileResult.Rows)
        {
            var columnName = row.Field<string>("ColumnName") ?? string.Empty;
            var nullCount = row.Field<long>("NullCount");
            
            var profile = new ColumnProfile
            {
                ColumnName = columnName,
                DataType = row.Field<string>("DataType") ?? string.Empty,
                DistinctCount = row.Field<long>("DistinctCount"),
                NullCount = nullCount,
                NullPercentage = rowCount > 0 ? (double)nullCount / rowCount * 100 : 0,
                MinValue = row["MinValue"] != DBNull.Value ? row["MinValue"] : null,
                MaxValue = row["MaxValue"] != DBNull.Value ? row["MaxValue"] : null,
                MeanValue = row["MeanValue"] != DBNull.Value ? row.Field<double>("MeanValue") : null,
                MedianValue = row["MedianValue"] != DBNull.Value ? row.Field<double>("MedianValue") : null,
                ModeValue = row["ModeValue"] != DBNull.Value ? row["ModeValue"] : null,
                StandardDeviation = row["StandardDeviation"] != DBNull.Value ? row.Field<double>("StandardDeviation") : null
            };
            
            columnProfiles[columnName] = profile;
        }

        // Get top values if requested
        if (options.IncludeTopValues)
        {
            var topValuesParams = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@TopCount", options.TopValuesCount)
            };

            var topValuesResult = await _executor.ExecuteAsync("sp_GetTopValues", topValuesParams, cancellationToken);
            
            foreach (DataRow row in topValuesResult.Rows)
            {
                var columnName = row.Field<string>("ColumnName") ?? string.Empty;
                var value = row.Field<string>("Value") ?? string.Empty;
                var count = row.Field<long>("Count");
                
                if (columnProfiles.ContainsKey(columnName))
                {
                    columnProfiles[columnName].TopValues[value] = count;
                }
            }
        }

        return new DataProfileResult
        {
            TableName = tableName,
            RowCount = rowCount,
            Columns = columnProfiles,
            Metrics = metrics,
            ProfiledAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<PatternDetectionResult> DetectPatternsAsync(string database, string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("Column name is required", nameof(columnName));

        _logger.LogInformation("Detecting patterns for column {ColumnName} in table {TableName}", columnName, tableName);

        // Detect patterns
        var patternParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@ColumnName", columnName)
        };

        var patternResult = await _executor.ExecuteAsync("sp_DetectPatterns", patternParams, cancellationToken);
        
        var patterns = new List<Pattern>();
        
        foreach (DataRow row in patternResult.Rows)
        {
            var examples = row.Field<string>("Examples")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            
            patterns.Add(new Pattern
            {
                Name = row.Field<string>("PatternName") ?? string.Empty,
                RegexPattern = row.Field<string>("RegexPattern") ?? string.Empty,
                MatchPercentage = row.Field<double>("MatchPercentage"),
                Examples = examples,
                Description = row.Field<string>("Description") ?? string.Empty
            });
        }

        // Get data type recommendations
        var typeParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@ColumnName", columnName)
        };

        var typeResult = await _executor.ExecuteAsync("sp_RecommendDataTypes", typeParams, cancellationToken);
        
        var recommendations = new List<DataTypeRecommendation>();
        
        foreach (DataRow row in typeResult.Rows)
        {
            recommendations.Add(new DataTypeRecommendation
            {
                CurrentType = row.Field<string>("CurrentType") ?? string.Empty,
                RecommendedType = row.Field<string>("RecommendedType") ?? string.Empty,
                Reason = row.Field<string>("Reason") ?? string.Empty,
                ConfidenceScore = row.Field<double>("ConfidenceScore"),
                SpaceSavingsBytes = row["SpaceSavingsBytes"] != DBNull.Value ? row.Field<long>("SpaceSavingsBytes") : null
            });
        }

        return new PatternDetectionResult
        {
            ColumnName = columnName,
            DetectedPatterns = patterns.ToArray(),
            TypeRecommendations = recommendations.ToArray()
        };
    }

    public async Task<DataQualityResult> AnalyzeDataQualityAsync(string database, string tableName, QualityRules rules, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        _logger.LogInformation("Analyzing data quality for table {TableName} in database {Database}", tableName, database);

        // Analyze quality issues
        var qualityParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@Rules", JsonSerializer.Serialize(rules))
        };

        var qualityResult = await _executor.ExecuteAsync("sp_AnalyzeDataQuality", qualityParams, cancellationToken);
        
        var issues = new List<QualityIssue>();
        
        foreach (DataRow row in qualityResult.Rows)
        {
            issues.Add(new QualityIssue
            {
                Type = row.Field<string>("IssueType") ?? string.Empty,
                Severity = row.Field<string>("Severity") ?? string.Empty,
                Column = row.Field<string>("Column") ?? string.Empty,
                Description = row.Field<string>("Description") ?? string.Empty,
                AffectedRows = row.Field<long>("AffectedRows"),
                SampleQuery = row.Field<string>("SampleQuery") ?? string.Empty
            });
        }

        // Get quality metrics per column
        var metricsParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName)
        };

        var metricsResult = await _executor.ExecuteAsync("sp_GetQualityMetrics", metricsParams, cancellationToken);
        
        var columnMetrics = new Dictionary<string, QualityMetrics>();
        
        foreach (DataRow row in metricsResult.Rows)
        {
            var column = row.Field<string>("Column") ?? string.Empty;
            columnMetrics[column] = new QualityMetrics
            {
                CompletenessScore = row.Field<double>("CompletenessScore"),
                ConsistencyScore = row.Field<double>("ConsistencyScore"),
                ValidityScore = row.Field<double>("ValidityScore"),
                UniquenessScore = row.Field<double>("UniquenessScore"),
                AccuracyScore = row.Field<double>("AccuracyScore")
            };
        }

        // Calculate overall quality score
        var overallScore = 0.0;
        if (columnMetrics.Count > 0)
        {
            var allScores = columnMetrics.Values.SelectMany(m => new[] 
            { 
                m.CompletenessScore, 
                m.ConsistencyScore, 
                m.ValidityScore, 
                m.UniquenessScore, 
                m.AccuracyScore 
            });
            overallScore = allScores.Average();
        }

        // Generate recommendations
        var recommendations = new List<string>();
        
        foreach (var issue in issues.Where(i => i.Severity == "High"))
        {
            recommendations.Add($"Address {issue.Type} issue in column {issue.Column}: {issue.Description}");
        }
        
        if (!recommendations.Any())
        {
            recommendations.Add("No critical data quality issues found. Consider regular monitoring.");
        }

        return new DataQualityResult
        {
            OverallQualityScore = overallScore,
            Issues = issues.ToArray(),
            ColumnMetrics = columnMetrics,
            Recommendations = recommendations.ToArray()
        };
    }

    public async Task<DataInsightsResult> GenerateInsightsAsync(string database, string query, InsightOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Generating insights for query on database {Database}", database);

        var startTime = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventTime = startTime,
            UserId = "system", // TODO: Get from context
            UserName = "System",
            Action = "GenerateInsights",
            EntityType = "Analytics",
            EntityId = database,
            AdditionalData = $"Query: {query.Substring(0, Math.Min(query.Length, 500))}"
        };

        try
        {
            // Generate insights
            var insightParams = new[]
            {
                new SqlParameter("@Database", database),
                new SqlParameter("@Query", query),
                new SqlParameter("@Options", JsonSerializer.Serialize(options))
            };

            var insightResult = await _executor.ExecuteAsync("sp_GenerateInsights", insightParams, cancellationToken);
            
            var insights = new List<Insight>();
            
            foreach (DataRow row in insightResult.Rows)
            {
                insights.Add(new Insight
                {
                    Type = row.Field<string>("Type") ?? string.Empty,
                    Title = row.Field<string>("Title") ?? string.Empty,
                    Description = row.Field<string>("Description") ?? string.Empty,
                    SignificanceScore = row.Field<double>("SignificanceScore"),
                    VisualizationType = row.Field<string>("VisualizationType") ?? string.Empty
                });
            }

            // Get correlations if requested
            var correlations = new List<Correlation>();
            if (options.IncludeCorrelations)
            {
                var correlationResult = await _executor.ExecuteAsync("sp_FindCorrelations", insightParams, cancellationToken);
                
                foreach (DataRow row in correlationResult.Rows)
                {
                    correlations.Add(new Correlation
                    {
                        Column1 = row.Field<string>("Column1") ?? string.Empty,
                        Column2 = row.Field<string>("Column2") ?? string.Empty,
                        CorrelationCoefficient = row.Field<double>("CorrelationCoefficient"),
                        CorrelationType = row.Field<string>("CorrelationType") ?? string.Empty,
                        PValue = row.Field<double>("PValue")
                    });
                }
            }

            // Get trends if requested
            var trends = new List<Trend>();
            if (options.IncludeTrends)
            {
                var trendResult = await _executor.ExecuteAsync("sp_AnalyzeTrends", insightParams, cancellationToken);
                
                foreach (DataRow row in trendResult.Rows)
                {
                    trends.Add(new Trend
                    {
                        Column = row.Field<string>("Column") ?? string.Empty,
                        TrendType = row.Field<string>("TrendType") ?? string.Empty,
                        Slope = row.Field<double>("Slope"),
                        R2Score = row.Field<double>("R2Score"),
                        TimeGranularity = row.Field<string>("TimeGranularity") ?? string.Empty
                    });
                }
            }

            // Get outliers if requested
            var outliers = new List<Outlier>();
            if (options.IncludeOutliers)
            {
                var outlierResult = await _executor.ExecuteAsync("sp_DetectOutliers", insightParams, cancellationToken);
                
                foreach (DataRow row in outlierResult.Rows)
                {
                    outliers.Add(new Outlier
                    {
                        Column = row.Field<string>("Column") ?? string.Empty,
                        Value = row["Value"],
                        ZScore = row.Field<double>("ZScore"),
                        Method = row.Field<string>("Method") ?? string.Empty,
                        Query = row.Field<string>("Query") ?? string.Empty
                    });
                }
            }

            auditEvent.Success = true;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            return new DataInsightsResult
            {
                Insights = insights.ToArray(),
                Correlations = correlations.ToArray(),
                Trends = trends.ToArray(),
                Outliers = outliers.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insights for database {Database}", database);
            
            auditEvent.Success = false;
            auditEvent.ErrorMessage = ex.Message;
            auditEvent.Duration = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            await _unitOfWork.AuditEvents.AddAsync(auditEvent, cancellationToken);

            throw;
        }
    }

    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(string database, string tableName, AnomalyDetectionOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Detecting anomalies in table {TableName} using {Method} method", tableName, options.Method);

        // Detect anomalies
        var anomalyParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName),
            new SqlParameter("@Options", JsonSerializer.Serialize(options))
        };

        var anomalyResult = await _executor.ExecuteAsync("sp_DetectAnomalies", anomalyParams, cancellationToken);
        
        var anomalies = new List<Anomaly>();
        
        foreach (DataRow row in anomalyResult.Rows)
        {
            var detectedAt = row["DetectedAt"] != DBNull.Value 
                ? new DateTimeOffset(row.Field<DateTime>("DetectedAt"), TimeSpan.Zero) 
                : (DateTimeOffset?)null;
            
            anomalies.Add(new Anomaly
            {
                Type = row.Field<string>("Type") ?? string.Empty,
                Description = row.Field<string>("Description") ?? string.Empty,
                AnomalyScore = row.Field<double>("AnomalyScore"),
                DetectedAt = detectedAt,
                Query = row.Field<string>("Query") ?? string.Empty
            });
        }

        // Get statistics
        var statsParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@TableName", tableName)
        };

        var statsResult = await _executor.ExecuteAsync("sp_GetAnomalyStatistics", statsParams, cancellationToken);
        
        var statistics = new AnomalyStatistics();
        if (statsResult.Rows.Count > 0)
        {
            var statsRow = statsResult.Rows[0];
            statistics.TotalAnomalies = statsRow.Field<int>("TotalAnomalies");
            statistics.AnomalyRate = statsRow.Field<double>("AnomalyRate");
            
            if (statsRow["FirstAnomaly"] != DBNull.Value)
            {
                statistics.FirstAnomaly = new DateTimeOffset(statsRow.Field<DateTime>("FirstAnomaly"), TimeSpan.Zero);
            }
            
            if (statsRow["LastAnomaly"] != DBNull.Value)
            {
                statistics.LastAnomaly = new DateTimeOffset(statsRow.Field<DateTime>("LastAnomaly"), TimeSpan.Zero);
            }
        }

        // Group anomalies by type
        statistics.AnomaliesByType = anomalies
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get affected columns
        var affectedColumns = options.SpecificColumns ?? Array.Empty<string>();

        return new AnomalyDetectionResult
        {
            Anomalies = anomalies.ToArray(),
            Statistics = statistics,
            AffectedColumns = affectedColumns
        };
    }

    public async Task<VisualizationRecommendation> RecommendVisualizationsAsync(string database, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required", nameof(database));
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));

        _logger.LogInformation("Recommending visualizations for query on database {Database}", database);

        // Analyze data characteristics
        var charParams = new[]
        {
            new SqlParameter("@Database", database),
            new SqlParameter("@Query", query)
        };

        var charResult = await _executor.ExecuteAsync("sp_AnalyzeDataCharacteristics", charParams, cancellationToken);
        
        var characteristics = new DataCharacteristics();
        if (charResult.Rows.Count > 0)
        {
            var charRow = charResult.Rows[0];
            characteristics = new DataCharacteristics
            {
                DataType = charRow.Field<string>("DataType") ?? string.Empty,
                DimensionCount = charRow.Field<int>("DimensionCount"),
                MeasureCount = charRow.Field<int>("MeasureCount"),
                HasTimeSeries = charRow.Field<bool>("HasTimeSeries"),
                HasCategories = charRow.Field<bool>("HasCategories"),
                HasGeospatial = charRow.Field<bool>("HasGeospatial")
            };
        }

        // Get visualization recommendations
        var recommendResult = await _executor.ExecuteAsync("sp_RecommendVisualizations", charParams, cancellationToken);
        
        var recommendations = new List<ChartRecommendation>();
        
        foreach (DataRow row in recommendResult.Rows)
        {
            var configuration = new ChartConfiguration
            {
                XAxis = row.Field<string>("XAxis") ?? string.Empty,
                YAxis = row.Field<string>("YAxis") ?? string.Empty,
                GroupBy = row["GroupBy"] != DBNull.Value ? row.Field<string>("GroupBy") : null,
                ColorBy = row["ColorBy"] != DBNull.Value ? row.Field<string>("ColorBy") : null
            };
            
            recommendations.Add(new ChartRecommendation
            {
                ChartType = row.Field<string>("ChartType") ?? string.Empty,
                Reason = row.Field<string>("Reason") ?? string.Empty,
                SuitabilityScore = row.Field<double>("SuitabilityScore"),
                Configuration = configuration
            });
        }

        return new VisualizationRecommendation
        {
            Recommendations = recommendations.ToArray(),
            Characteristics = characteristics
        };
    }
}