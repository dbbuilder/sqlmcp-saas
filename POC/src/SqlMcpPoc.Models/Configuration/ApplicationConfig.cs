using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SqlMcpPoc.Models.Configuration
{
    /// <summary>
    /// Root configuration model for the SQLMCP POC application
    /// Contains all configuration sections required for application operation
    /// </summary>
    public class ApplicationConfig
    {
        [JsonPropertyName("LLM_Platform")]
        [Required]
        public string LlmPlatform { get; set; } = string.Empty;

        [JsonPropertyName("OpenAI_Config")]
        public OpenAIConfig? OpenAIConfig { get; set; }

        [JsonPropertyName("Database_Platform")]
        [Required]
        public string DatabasePlatform { get; set; } = string.Empty;

        [JsonPropertyName("SQLServer_Config")]
        public SqlServerConfig? SqlServerConfig { get; set; }

        [JsonPropertyName("Safety_Check_Config")]
        public SafetyCheckConfig SafetyCheckConfig { get; set; } = new();

        [JsonPropertyName("Logging_Config")]
        public LoggingConfig LoggingConfig { get; set; } = new();

        [JsonPropertyName("Resilience_Config")]
        public ResilienceConfig ResilienceConfig { get; set; } = new();

        /// <summary>
        /// Validates the configuration and throws exceptions for invalid settings
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LlmPlatform))
                throw new InvalidOperationException("LLM_Platform configuration is required");

            if (string.IsNullOrWhiteSpace(DatabasePlatform))
                throw new InvalidOperationException("Database_Platform configuration is required");

            if (LlmPlatform.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                if (OpenAIConfig == null)
                    throw new InvalidOperationException("OpenAI_Config is required when LLM_Platform is OpenAI");
                
                OpenAIConfig.Validate();
            }

            if (DatabasePlatform.Equals("SQLServer", StringComparison.OrdinalIgnoreCase))
            {
                if (SqlServerConfig == null)
                    throw new InvalidOperationException("SQLServer_Config is required when Database_Platform is SQLServer");
                
                SqlServerConfig.Validate();
            }

            SafetyCheckConfig.Validate();
            LoggingConfig.Validate();
            ResilienceConfig.Validate();
        }
    }
}
