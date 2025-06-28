using System.Text.RegularExpressions;

namespace DatabaseAutomationPlatform.Api.Services;

/// <summary>
/// Implementation of request validation service
/// </summary>
public class RequestValidationService : IRequestValidationService
{
    private readonly ILogger<RequestValidationService> _logger;
    private readonly HashSet<string> _dangerousSqlPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE", 
        "INSERT", "UPDATE", "MERGE", "GRANT", "REVOKE", "DENY"
    };

    public RequestValidationService(ILogger<RequestValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ValidationResult> ValidateRequestAsync(McpRequest request)
    {
        if (request == null)
        {
            return Task.FromResult(ValidationResult.Failure("Request cannot be null"));
        }

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.JsonRpc) || request.JsonRpc != "2.0")
        {
            errors.Add(new ValidationError { Field = "jsonrpc", Message = "Invalid JSON-RPC version" });
        }

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            errors.Add(new ValidationError { Field = "method", Message = "Method is required" });
        }

        if (request.Id == null)
        {
            errors.Add(new ValidationError { Field = "id", Message = "Request ID is required" });
        }

        return Task.FromResult(errors.Any() 
            ? ValidationResult.Failure(errors) 
            : ValidationResult.Success());
    }

    public Task<ValidationResult> ValidateToolParametersAsync(string toolName, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Task.FromResult(ValidationResult.Failure("Tool name is required"));
        }

        if (parameters == null)
        {
            return Task.FromResult(ValidationResult.Failure("Parameters cannot be null"));
        }

        var errors = new List<ValidationError>();

        // Validate based on tool name
        switch (toolName.ToLower())
        {
            case "query":
                ValidateQueryParameters(parameters, errors);
                break;
            case "execute":
                ValidateExecuteParameters(parameters, errors);
                break;
            case "schema":
                ValidateSchemaParameters(parameters, errors);
                break;
            case "analyze":
                ValidateAnalyzeParameters(parameters, errors);
                break;
            default:
                errors.Add(new ValidationError { Message = $"Unknown tool: {toolName}" });
                break;
        }

        return Task.FromResult(errors.Any() 
            ? ValidationResult.Failure(errors) 
            : ValidationResult.Success());
    }

    public Task<ValidationResult> ValidateSqlQueryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(ValidationResult.Failure("Query cannot be empty"));
        }

        var errors = new List<ValidationError>();

        // Check for SQL injection patterns
        if (ContainsSqlInjectionPattern(query))
        {
            errors.Add(new ValidationError 
            { 
                Field = "query", 
                Message = "Query contains potential SQL injection patterns",
                Code = "SQL_INJECTION_RISK"
            });
        }

        // Check query length
        if (query.Length > 10000)
        {
            errors.Add(new ValidationError 
            { 
                Field = "query", 
                Message = "Query exceeds maximum length of 10000 characters",
                Code = "QUERY_TOO_LONG"
            });
        }

        // For read-only queries, check for dangerous operations
        if (ContainsDangerousOperation(query))
        {
            errors.Add(new ValidationError 
            { 
                Field = "query", 
                Message = "Query contains potentially dangerous operations",
                Code = "DANGEROUS_OPERATION"
            });
        }

        return Task.FromResult(errors.Any() 
            ? ValidationResult.Failure(errors) 
            : ValidationResult.Success());
    }

    private void ValidateQueryParameters(Dictionary<string, object> parameters, List<ValidationError> errors)
    {
        if (!parameters.ContainsKey("database") || string.IsNullOrWhiteSpace(parameters["database"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "database", Message = "Database name is required" });
        }

        if (!parameters.ContainsKey("query") || string.IsNullOrWhiteSpace(parameters["query"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "query", Message = "Query is required" });
        }

        if (parameters.TryGetValue("timeout", out var timeout))
        {
            if (!int.TryParse(timeout?.ToString(), out var timeoutValue) || timeoutValue < 1 || timeoutValue > 300)
            {
                errors.Add(new ValidationError { Field = "timeout", Message = "Timeout must be between 1 and 300 seconds" });
            }
        }
    }

    private void ValidateExecuteParameters(Dictionary<string, object> parameters, List<ValidationError> errors)
    {
        if (!parameters.ContainsKey("database") || string.IsNullOrWhiteSpace(parameters["database"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "database", Message = "Database name is required" });
        }

        if (!parameters.ContainsKey("command") || string.IsNullOrWhiteSpace(parameters["command"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "command", Message = "Command is required" });
        }
    }

    private void ValidateSchemaParameters(Dictionary<string, object> parameters, List<ValidationError> errors)
    {
        if (!parameters.ContainsKey("database") || string.IsNullOrWhiteSpace(parameters["database"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "database", Message = "Database name is required" });
        }

        if (!parameters.ContainsKey("objectType") || string.IsNullOrWhiteSpace(parameters["objectType"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "objectType", Message = "Object type is required" });
        }

        var validObjectTypes = new[] { "table", "view", "procedure", "function", "index" };
        if (parameters.TryGetValue("objectType", out var objectType) && 
            !validObjectTypes.Contains(objectType?.ToString()?.ToLower()))
        {
            errors.Add(new ValidationError 
            { 
                Field = "objectType", 
                Message = $"Invalid object type. Must be one of: {string.Join(", ", validObjectTypes)}" 
            });
        }
    }

    private void ValidateAnalyzeParameters(Dictionary<string, object> parameters, List<ValidationError> errors)
    {
        if (!parameters.ContainsKey("database") || string.IsNullOrWhiteSpace(parameters["database"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "database", Message = "Database name is required" });
        }

        if (!parameters.ContainsKey("analysisType") || string.IsNullOrWhiteSpace(parameters["analysisType"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "analysisType", Message = "Analysis type is required" });
        }

        if (!parameters.ContainsKey("target") || string.IsNullOrWhiteSpace(parameters["target"]?.ToString()))
        {
            errors.Add(new ValidationError { Field = "target", Message = "Target is required" });
        }

        var validAnalysisTypes = new[] { "performance", "statistics", "patterns", "security" };
        if (parameters.TryGetValue("analysisType", out var analysisType) && 
            !validAnalysisTypes.Contains(analysisType?.ToString()?.ToLower()))
        {
            errors.Add(new ValidationError 
            { 
                Field = "analysisType", 
                Message = $"Invalid analysis type. Must be one of: {string.Join(", ", validAnalysisTypes)}" 
            });
        }
    }

    private bool ContainsSqlInjectionPattern(string query)
    {
        // Common SQL injection patterns
        var patterns = new[]
        {
            @"(\b(UNION|SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE)\b.*\b(FROM|WHERE|SET|VALUES|INTO)\b)",
            @"(--|\#|\/\*|\*\/)",  // SQL comments
            @"(\bOR\b\s*\d+\s*=\s*\d+)", // OR 1=1
            @"(\bAND\b\s*\d+\s*=\s*\d+)", // AND 1=1
            @"(;|\bGO\b)", // Statement terminators
            @"(\bEXEC\b|\bEXECUTE\b)\s*\(", // Dynamic SQL execution
            @"(xp_|sp_)", // System stored procedures
        };

        return patterns.Any(pattern => Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase));
    }

    private bool ContainsDangerousOperation(string query)
    {
        var queryUpper = query.ToUpperInvariant();
        return _dangerousSqlPatterns.Any(pattern => queryUpper.Contains(pattern));
    }
}