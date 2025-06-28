namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Data sensitivity levels for classification
    /// </summary>
    public enum DataSensitivityLevel
    {
        /// <summary>
        /// Public data - no restrictions on access or use
        /// </summary>
        Public,

        /// <summary>
        /// Internal data - accessible to company employees only
        /// </summary>
        Internal,

        /// <summary>
        /// Confidential data - accessible to authorized personnel only
        /// </summary>
        Confidential,

        /// <summary>
        /// Restricted data - requires specific approval for access
        /// </summary>
        Restricted,

        /// <summary>
        /// Personal data (PII) - special handling required for privacy
        /// </summary>
        Personal,

        /// <summary>
        /// Financial data - regulatory compliance required
        /// </summary>
        Financial,

        /// <summary>
        /// Healthcare data (PHI) - HIPAA compliance required
        /// </summary>
        Healthcare
    }

    /// <summary>
    /// Categories of data for classification purposes
    /// </summary>
    public enum DataCategory
    {
        /// <summary>
        /// Personally identifiable information
        /// </summary>
        PersonallyIdentifiable,

        /// <summary>
        /// Financial information and records
        /// </summary>
        FinancialInformation,

        /// <summary>
        /// Protected health information
        /// </summary>
        HealthInformation,

        /// <summary>
        /// Credit card and payment data
        /// </summary>
        CreditCardData,

        /// <summary>
        /// Social security numbers
        /// </summary>
        SocialSecurityNumber,

        /// <summary>
        /// Biometric data
        /// </summary>
        BiometricData,

        /// <summary>
        /// Location and GPS data
        /// </summary>
        LocationData,

        /// <summary>
        /// Communication data (emails, messages)
        /// </summary>
        CommunicationData,

        /// <summary>
        /// Behavioral and usage data
        /// </summary>
        BehavioralData,

        /// <summary>
        /// Technical and system data
        /// </summary>
        TechnicalData,

        /// <summary>
        /// Government identification numbers
        /// </summary>
        GovernmentId,

        /// <summary>
        /// Intellectual property
        /// </summary>
        IntellectualProperty,

        /// <summary>
        /// Trade secrets and confidential business information
        /// </summary>
        TradeSecrets,

        /// <summary>
        /// Legal and contractual information
        /// </summary>
        LegalInformation
    }
}
