using System.Text.Json.Serialization;

namespace DatabaseAutomationPlatform.Api.Models;

/// <summary>
/// Standard error response model following RFC 7807 (Problem Details for HTTP APIs)
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// A URI reference that identifies the problem type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "about:blank";

    /// <summary>
    /// A short, human-readable summary of the problem type
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "An error occurred";

    /// <summary>
    /// The HTTP status code
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence of the problem
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    /// <summary>
    /// Correlation ID for tracking this error across systems
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Validation errors (for 400 Bad Request responses)
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Additional problem-specific extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}