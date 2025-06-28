using System.ComponentModel.DataAnnotations;

namespace DatabaseAutomationPlatform.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// Configuration options for logging infrastructure
    /// </summary>
    public class LoggingOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "Logging";

        /// <summary>
        /// Application Insights instrumentation key
        /// </summary>
        [Required]
        public string ApplicationInsightsInstrumentationKey { get; set; } = string.Empty;

        /// <summary>
        /// Application Insights connection string (preferred over instrumentation key)
        /// </summary>
        public string? ApplicationInsightsConnectionString { get; set; }

        /// <summary>
        /// Minimum log level for general logging
        /// </summary>
        public string MinimumLevel { get; set; } = "Information";

        /// <summary>
        /// Minimum log level for Microsoft namespace
        /// </summary>
        public string MicrosoftMinimumLevel { get; set; } = "Warning";

        /// <summary>
        /// Minimum log level for System namespace
        /// </summary>
        public string SystemMinimumLevel { get; set; } = "Warning";

        /// <summary>
        /// Enable console logging
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = false;

        /// <summary>
        /// File logging path
        /// </summary>
        public string FileLoggingPath { get; set; } = "logs/log-.txt";

        /// <summary>
        /// Rolling interval for file logs
        /// </summary>
        public string RollingInterval { get; set; } = "Day";

        /// <summary>
        /// Retention days for file logs
        /// </summary>
        public int? RetainedFileCountLimit { get; set; } = 31;

        /// <summary>
        /// Enable structured logging
        /// </summary>
        public bool EnableStructuredLogging { get; set; } = true;

        /// <summary>
        /// Enable sensitive data logging (should be false in production)
        /// </summary>
        public bool EnableSensitiveDataLogging { get; set; } = false;

        /// <summary>
        /// Enable request/response logging
        /// </summary>
        public bool EnableRequestResponseLogging { get; set; } = true;

        /// <summary>
        /// Maximum request body size to log (in bytes)
        /// </summary>
        public int MaxRequestBodySize { get; set; } = 32768; // 32KB

        /// <summary>
        /// Enable performance logging
        /// </summary>
        public bool EnablePerformanceLogging { get; set; } = true;

        /// <summary>
        /// Performance logging threshold in milliseconds
        /// </summary>
        public int PerformanceLoggingThreshold { get; set; } = 1000;

        /// <summary>
        /// Enable security event logging
        /// </summary>
        public bool EnableSecurityEventLogging { get; set; } = true;

        /// <summary>
        /// Enable correlation ID enrichment
        /// </summary>
        public bool EnableCorrelationId { get; set; } = true;

        /// <summary>
        /// Enable machine name enrichment
        /// </summary>
        public bool EnableMachineName { get; set; } = true;

        /// <summary>
        /// Enable environment name enrichment
        /// </summary>
        public bool EnableEnvironmentName { get; set; } = true;

        /// <summary>
        /// Custom properties to add to all log entries
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        /// <summary>
        /// Validates the configuration options
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApplicationInsightsInstrumentationKey) && 
                string.IsNullOrWhiteSpace(ApplicationInsightsConnectionString))
            {
                throw new ValidationException(
                    "Either ApplicationInsightsInstrumentationKey or ApplicationInsightsConnectionString must be provided");
            }

            if (EnableFileLogging && string.IsNullOrWhiteSpace(FileLoggingPath))
            {
                throw new ValidationException("FileLoggingPath must be provided when EnableFileLogging is true");
            }

            if (MaxRequestBodySize < 0)
            {
                throw new ValidationException("MaxRequestBodySize must be non-negative");
            }

            if (PerformanceLoggingThreshold < 0)
            {
                throw new ValidationException("PerformanceLoggingThreshold must be non-negative");
            }
        }
    }
}