namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Service for validating incoming requests
/// </summary>
public interface IRequestValidationService
{
    /// <summary>
    /// Validates an MCP request
    /// </summary>
    Task<ValidationResult> ValidateRequestAsync(McpRequest request);

    /// <summary>
    /// Validates tool parameters
    /// </summary>
    Task<ValidationResult> ValidateToolParametersAsync(string toolName, Dictionary<string, object> parameters);

    /// <summary>
    /// Validates SQL query for security issues
    /// </summary>
    Task<ValidationResult> ValidateSqlQueryAsync(string query);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(string error) => new() 
    { 
        IsValid = false, 
        Errors = new List<ValidationError> { new ValidationError { Message = error } } 
    };
    
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };
}

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
}