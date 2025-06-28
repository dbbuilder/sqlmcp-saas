using System;
using System.Collections.Generic;

namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Represents privacy requirements and protection measures for data
    /// </summary>
    public class PrivacyRequirements
    {
        /// <summary>
        /// Whether user consent is required to process this data
        /// </summary>
        public bool RequiresConsent { get; set; }

        /// <summary>
        /// Whether data must be anonymized before use
        /// </summary>
        public bool RequiresAnonymization { get; set; }

        /// <summary>
        /// Whether data must be pseudonymized before use
        /// </summary>
        public bool RequiresPseudonymization { get; set; }

        /// <summary>
        /// Whether data must be encrypted at rest and in transit
        /// </summary>
        public bool RequiresEncryption { get; set; }

        /// <summary>
        /// Whether all access to this data must be audited
        /// </summary>
        public bool RequiresAuditLog { get; set; }

        /// <summary>
        /// Whether data has retention period requirements
        /// </summary>
        public bool RequiresRetentionPolicy { get; set; }

        /// <summary>
        /// Whether "right to be forgotten" applies to this data
        /// </summary>
        public bool RequiresRightToBeForgotten { get; set; }

        /// <summary>
        /// Whether data access requires user authentication
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Whether data access requires specific authorization
        /// </summary>
        public bool RequiresAuthorization { get; set; }

        /// <summary>
        /// Whether data must be masked when displayed to unauthorized users
        /// </summary>
        public bool RequiresMasking { get; set; }

        /// <summary>
        /// Legal basis for processing this data
        /// </summary>
        public List<string> LegalBasis { get; set; } = new();

        /// <summary>
        /// Compliance requirements that apply to this data
        /// </summary>
        public List<string> ComplianceRequirements { get; set; } = new();

        /// <summary>
        /// Retention period for this data
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; }

        /// <summary>
        /// Geographic restrictions on where this data can be stored
        /// </summary>
        public List<string> GeographicRestrictions { get; set; } = new();

        /// <summary>
        /// Purposes for which this data can be used
        /// </summary>
        public List<string> AllowedPurposes { get; set; } = new();

        /// <summary>
        /// Roles or groups that can access this data
        /// </summary>
        public List<string> AuthorizedRoles { get; set; } = new();

        /// <summary>
        /// Minimum access level required to view this data
        /// </summary>
        public string? MinimumAccessLevel { get; set; }

        /// <summary>
        /// Additional privacy controls and requirements
        /// </summary>
        public Dictionary<string, object> AdditionalControls { get; set; } = new();

        /// <summary>
        /// Breach notification requirements
        /// </summary>
        public BreachNotificationRequirements? BreachNotification { get; set; }
    }

    /// <summary>
    /// Requirements for breach notification
    /// </summary>
    public class BreachNotificationRequirements
    {
        /// <summary>
        /// Whether breach notification is required for this data
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Time limit for notification after breach discovery
        /// </summary>
        public TimeSpan NotificationTimeLimit { get; set; }

        /// <summary>
        /// Authorities that must be notified
        /// </summary>
        public List<string> NotificationAuthorities { get; set; } = new();

        /// <summary>
        /// Whether affected individuals must be notified
        /// </summary>
        public bool NotifyIndividuals { get; set; }

        /// <summary>
        /// Threshold for individual notification (number of records)
        /// </summary>
        public int? IndividualNotificationThreshold { get; set; }
    }
}
