using System;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when configuration is missing or invalid.
    /// </summary>
    public class ConfigurationException : BaseException
    {
        /// <summary>
        /// Gets the configuration key that caused the exception.
        /// </summary>
        public string ConfigurationKey { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationException class.
        /// </summary>
        /// <param name="configurationKey">The missing or invalid configuration key.</param>
        public ConfigurationException(string configurationKey) 
            : base(
                $"Configuration key '{configurationKey}' is missing or invalid", 
                "The application is not configured correctly. Please contact support.")
        {
            ConfigurationKey = configurationKey;
            AddDetail("ConfigurationKey", configurationKey);
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationException class with a custom message.
        /// </summary>
        /// <param name="configurationKey">The missing or invalid configuration key.</param>
        /// <param name="message">The detailed error message.</param>
        public ConfigurationException(string configurationKey, string message) 
            : base(message, "The application is not configured correctly. Please contact support.")
        {
            ConfigurationKey = configurationKey;
            AddDetail("ConfigurationKey", configurationKey);
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationException class with an inner exception.
        /// </summary>
        /// <param name="configurationKey">The missing or invalid configuration key.</param>
        /// <param name="message">The detailed error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ConfigurationException(string configurationKey, string message, Exception innerException) 
            : base(message, "The application is not configured correctly. Please contact support.", innerException)
        {
            ConfigurationKey = configurationKey;
            AddDetail("ConfigurationKey", configurationKey);
        }
    }
}
