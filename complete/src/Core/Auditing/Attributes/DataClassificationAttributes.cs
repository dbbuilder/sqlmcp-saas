using System;

namespace SqlMcp.Core.Auditing.Attributes
{
    /// <summary>
    /// Data sensitivity levels
    /// </summary>
    public enum DataSensitivity
    {
        /// <summary>
        /// Public data
        /// </summary>
        Public = 0,

        /// <summary>
        /// Internal use only
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Confidential data
        /// </summary>
        Confidential = 2,

        /// <summary>
        /// Highly confidential data
        /// </summary>
        HighlyConfidential = 3,

        /// <summary>
        /// Restricted data (highest level)
        /// </summary>
        Restricted = 4
    }

    /// <summary>
    /// Marks a property or parameter as containing classified data
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field, 
        AllowMultiple = false)]
    public class DataClassificationAttribute : Attribute
    {
        /// <summary>
        /// Data sensitivity level
        /// </summary>
        public DataSensitivity Sensitivity { get; }

        /// <summary>
        /// Indicates if this is personally identifiable information
        /// </summary>
        public bool IsPii { get; }

        /// <summary>
        /// Indicates if this data is subject to GDPR
        /// </summary>
        public bool IsGdprData { get; }

        /// <summary>
        /// Custom classification tag
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Creates a new data classification attribute
        /// </summary>
        public DataClassificationAttribute(
            DataSensitivity sensitivity = DataSensitivity.Internal,
            bool isPii = false,
            bool isGdprData = false)
        {
            Sensitivity = sensitivity;
            IsPii = isPii;
            IsGdprData = isGdprData || isPii; // PII is always GDPR data
        }
    }

    /// <summary>
    /// Marks a property as containing personally identifiable information
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
    public class PersonalDataAttribute : DataClassificationAttribute
    {
        public PersonalDataAttribute() 
            : base(DataSensitivity.Confidential, isPii: true, isGdprData: true)
        {
        }
    }

    /// <summary>
    /// Marks a property as containing sensitive personal data (health, financial, etc.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
    public class SensitiveDataAttribute : DataClassificationAttribute
    {
        public SensitiveDataAttribute() 
            : base(DataSensitivity.HighlyConfidential, isPii: true, isGdprData: true)
        {
        }
    }

    /// <summary>
    /// Marks a property that should be masked in audit logs
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
    public class AuditMaskAttribute : Attribute
    {
        /// <summary>
        /// Masking strategy to use
        /// </summary>
        public MaskingStrategy Strategy { get; }

        /// <summary>
        /// Custom mask pattern (for Custom strategy)
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Creates a new audit mask attribute
        /// </summary>
        public AuditMaskAttribute(MaskingStrategy strategy = MaskingStrategy.Full)
        {
            Strategy = strategy;
        }
    }

    /// <summary>
    /// Strategies for masking sensitive data
    /// </summary>
    public enum MaskingStrategy
    {
        /// <summary>
        /// Replace entire value with asterisks
        /// </summary>
        Full,

        /// <summary>
        /// Show first few characters only
        /// </summary>
        Partial,

        /// <summary>
        /// Show first and last few characters
        /// </summary>
        Middle,

        /// <summary>
        /// Hash the value
        /// </summary>
        Hash,

        /// <summary>
        /// Custom pattern
        /// </summary>
        Custom
    }

    /// <summary>
    /// Marks a property that should not be included in audit logs
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
    public class AuditIgnoreAttribute : Attribute
    {
        /// <summary>
        /// Reason for ignoring (optional)
        /// </summary>
        public string Reason { get; set; }

        public AuditIgnoreAttribute(string reason = null)
        {
            Reason = reason;
        }
    }
}
