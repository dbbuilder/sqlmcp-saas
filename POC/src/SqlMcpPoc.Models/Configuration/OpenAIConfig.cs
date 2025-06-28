using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SqlMcpPoc.Models.Configuration
{
    /// <summary>
    /// Configuration for OpenAI LLM integration
    /// </summary>
    public class OpenAIConfig
    {
        [JsonPropertyName("ApiKey")]
        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("Model")]
        [Required]
        public string Model { get; set; } = "gpt-4-turbo";

        [JsonPropertyName("BaseUrl")]
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";

        [JsonPropertyName("MaxTokens")]
        public int MaxTokens { get; set; } = 4000;

        [JsonPropertyName("Temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("TimeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Validates the OpenAI configuration
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("OpenAI ApiKey is required");

            if (string.IsNullOrWhiteSpace(Model))
                throw new InvalidOperationException("OpenAI Model is required");

            if (string.IsNullOrWhiteSpace(BaseUrl))
                throw new InvalidOperationException("OpenAI BaseUrl is required");

            if (MaxTokens <= 0)
                throw new InvalidOperationException("OpenAI MaxTokens must be greater than 0");

            if (Temperature < 0 || Temperature > 2)
                throw new InvalidOperationException("OpenAI Temperature must be between 0 and 2");

            if (TimeoutSeconds <= 0)
                throw new InvalidOperationException("OpenAI TimeoutSeconds must be greater than 0");
        }
    }
}
