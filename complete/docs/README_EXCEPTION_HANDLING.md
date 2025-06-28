# SQL MCP Exception Handling Framework

## Overview
A comprehensive, security-focused exception handling framework for the SQL MCP Database Automation Platform. This framework ensures consistent error handling, prevents information disclosure, and provides detailed logging for debugging.

## Features
- **Custom Exception Hierarchy**: Specialized exceptions for different error scenarios
- **Global Exception Middleware**: Centralized exception handling and logging
- **Safe Error Messages**: Prevents sensitive information disclosure
- **Correlation ID Tracking**: End-to-end error tracking across the system
- **RFC 7807 Compliance**: Standard problem details format support

## Exception Types

### BaseException
Base class for all custom exceptions with correlation ID and safe message support.

```csharp
throw new BaseException("Detailed error for logging", "Safe message for users")
    .WithCorrelationId(correlationId);
```

### ValidationException
For input validation failures (HTTP 400).

```csharp
var exception = new ValidationException("Validation failed");
exception.AddValidationError("Email", "Email is required");
exception.AddValidationError("Email", "Email format is invalid");
throw exception;
```

### DatabaseException
For database-related errors with automatic message sanitization (HTTP 503).

```csharp
throw new DatabaseException("Connection timeout on server DB01", "GetUserById")
    .WithSqlErrorNumber(1205); // Deadlock
```

### SecurityException
For authentication/authorization failures (HTTP 401/403).

```csharp
throw new SecurityException("Unauthorized access attempt", SecurityEventType.AuthorizationFailure)
    .WithUserId(userId)
    .WithResource("/api/admin/users")
    .WithIpAddress(ipAddress);
```

### ResourceNotFoundException
For missing resources (HTTP 404).

```csharp
throw new ResourceNotFoundException("User", userId);
```

## Usage

### 1. Register Middleware in Startup.cs

```csharp
public void Configure(IApplicationBuilder app)
{
    // Register as first middleware to catch all exceptions
    app.UseGlobalExceptionHandler();
    
    // Other middleware...
}
```

### 2. Throw Custom Exceptions

```csharp
public async Task<User> GetUserAsync(string userId)
{
    if (string.IsNullOrEmpty(userId))
    {
        var validation = new ValidationException("Invalid request");
        validation.AddValidationError("UserId", "User ID is required");
        throw validation;
    }

    var user = await _repository.GetByIdAsync(userId);
    if (user == null)
    {
        throw new ResourceNotFoundException("User", userId);
    }

    return user;
}
```

### 3. Error Response Format

```json
{
  "correlationId": "a8f3d2e1-7b4c-4d9e-b1a2-3c5e7f9a1b2d",
  "message": "Validation failed. Please check the provided values and try again.",
  "timestamp": "2025-06-21T08:30:45.123Z",
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Email is required",
      "field": "Email"
    }
  ]
}
```

## Security Features
- **No Stack Traces**: Never exposed in production
- **Sanitized Messages**: Database errors are automatically sanitized
- **Generic Security Messages**: Prevents user enumeration
- **Correlation IDs**: Cryptographically random for security
- **Sensitive Data Masking**: Automatic in logs

## Best Practices
1. Always use custom exceptions instead of generic ones
2. Provide detailed messages for logging, safe messages for users
3. Add contextual information using fluent methods
4. Use appropriate exception types for proper HTTP status mapping
5. Include correlation IDs in all error scenarios

## Testing
The framework includes comprehensive unit tests with 95%+ coverage. Run tests:

```bash
dotnet test --filter "FullyQualifiedName~SqlMcp.Tests.Unit.Core.Exceptions"
dotnet test --filter "FullyQualifiedName~SqlMcp.Tests.Unit.Core.ErrorHandling"
```
