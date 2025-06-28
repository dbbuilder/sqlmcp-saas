using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents data classification and sensitivity information for database columns
    /// </summary>
    public class DataClassification
    {
        /// <summary>
        /// Unique identifier for this classification
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Database name
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Schema name
        /// </summary>
        public string SchemaName { get; set; } = string.Empty;

        /// <summary>
        /// Table name
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Column name
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the column
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Sensitivity level of the data
        /// </summary>
        public DataSensitivityLevel SensitivityLevel { get; set; }

        /// <summary>
        /// Categories that this data belongs to
        /// </summary>
        public List<DataCategory> Categories { get; set; } = new();

        /// <summary>
        /// Privacy requirements for this data
        /// </summary>
        public PrivacyRequirements PrivacyRequirements { get; set; } = new();

        /// <summary>
        /// Reason for this classification
        /// </summary>
        public string? ClassificationReason { get; set; }

        /// <summary>
        /// When this classification was created
        /// </summary>
        public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who classified this data
        /// </summary>
        public string ClassifiedBy { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was manually classified or auto-detected
        /// </summary>
        public bool IsManuallyClassified { get; set; }

        /// <summary>
        /// Confidence score for ML-based classification (0.0 to 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Sample values that led to this classification (anonymized)
        /// </summary>
        public List<string> SampleValues { get; set; } = new();

        /// <summary>
        /// Pattern or regex that matches this data type
        /// </summary>
        public string? DetectionPattern { get; set; }

        /// <summary>
        /// Last time this classification was reviewed
        /// </summary>
        public DateTime? LastReviewedAt { get; set; }

        /// <summary>
        /// Who last reviewed this classification
        /// </summary>
        public string? LastReviewedBy { get; set; }

        /// <summary>
        /// Whether this classification is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Additional metadata about the classification
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
