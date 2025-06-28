# Database Automation Platform (SQL MCP) - Enterprise Project Context

## CRITICAL: Read This First
You are working on an **ENTERPRISE-GRADE** Database Automation Platform that enables AI assistants to safely perform SQL operations through the Model Context Protocol (MCP). This is a production-ready system with strict security, compliance, and quality requirements.

**Project Location**: `D:\dev2\sqlmcp\complete\`
**Current Date**: 2025-06-21
**Project Phase**: Infrastructure Layer Development (90% complete)

## Enterprise Requirements (NON-NEGOTIABLE)

### 1. Security & Compliance
- **SQL Injection Prevention**: Every parameter must be sanitized using ParameterSanitizer
- **Audit Logging**: Every database operation must be logged with full audit trail
- **Security Events**: All suspicious activities must trigger security events
- **Safe Error Messages**: Never expose internal implementation details in errors
- **Circuit Breaker**: Prevent cascade failures with Polly circuit breaker pattern
- **Zero Trust**: Never trust user input, always validate and sanitize
- **Principle of Least Privilege**: Minimal permissions for all operations

### 2. Code Quality Standards
- **Test Coverage**: Minimum 95% code coverage (enforced)
- **Documentation**: XML comments on all public interfaces
- **Error Handling**: Comprehensive try-catch with specific error types
- **Logging**: Structured logging with Serilog at appropriate levels
- **Performance**: Sub-second response times for all operations
- **Scalability**: Support for high-concurrency scenarios

### 3. Technical Architecture
- **Framework**: .NET 8 with C# 12
- **Database**: SQL Server with T-SQL (no semicolons)
- **ORM**: Entity Framework Core (stored procedures only)- **Resilience**: Polly for retry policies and circuit breakers
- **Logging**: Serilog with Application Insights
- **Caching**: IMemoryCache for metadata
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **Testing**: xUnit with FluentAssertions and Moq

## User Preferences (MUST FOLLOW)

### Development Practices
1. **Complete Code Listings**: Always provide full code, never partial snippets
2. **Sequential Thinking**: Use sequential-thinking tool for planning
3. **No Circular Dependencies**: Design interfaces to prevent circular references
4. **Error Handling First**: Add error handling before implementing features
5. **Security by Design**: Consider security implications in every decision

### Code Style Requirements
- **T-SQL**: Never use semicolons in SQL statements
- **Dynamic SQL**: Always print dynamic SQL for debugging
- **Comments**: Inline comments for complex logic
- **File Organization**: One class per file, organized by feature
- **Async/Await**: Use async methods throughout

### Documentation Requirements
- Create REQUIREMENTS.md before implementation
- Create README.md for collaborators
- Create TODO.md with prioritized tasks
- Create FUTURE.md for enhancement ideas
- Update context documents after each session

## Project Status (As of 2025-06-21)

### ✅ Completed Infrastructure Components

#### INFRA-002: Database Connection Factory
- `IDbConnectionFactory.cs` - Interface definition
- `SqlConnectionFactory.cs` - Implementation with:
  - Connection pooling optimization
  - Azure Key Vault integration
  - Retry policies with Polly
  - Security logging
  - 95%+ test coverage achieved
#### INFRA-003: Serilog Logging Framework
- `LoggingConfiguration.cs` - Serilog setup
- `SecurityLogger.cs` - Security event logging
- `LoggingMiddleware.cs` - Request/response logging
- `CorrelationIdEnricher.cs` - Request correlation
- Application Insights integration
- 95%+ test coverage achieved

#### INFRA-004: Stored Procedure Executor (COMPLETED TODAY)
- `IStoredProcedureExecutor.cs` - Interface with 6 execution methods
- `StoredProcedureExecutor.cs` - 900+ lines implementation featuring:
  - **Retry Logic**: Exponential backoff with jitter for transient errors
  - **Circuit Breaker**: 5 failures trigger 30-second break
  - **Parameter Validation**: SQL injection prevention
  - **Audit Logging**: Every execution logged with metadata
  - **Output Parameters**: Full support for output/return values
  - **Transaction Support**: Commit/rollback with isolation levels
  - **Metadata Caching**: Stored procedure metadata cached
  - **Performance Tracking**: Execution time metrics

Supporting Classes:
- `StoredProcedureParameter.cs` - Type-safe parameter model
- `StoredProcedureResult<T>` - Generic result wrapper
- `StoredProcedureMetadata.cs` - Cached procedure info
- `ParameterSanitizer.cs` - SQL injection prevention

Test Coverage:
- 30+ unit test methods in `StoredProcedureExecutorTests.cs`
- Integration tests in `StoredProcedureExecutorIntegrationTests.cs`
- `DatabaseTestFixture.cs` for test infrastructure
- 95%+ code coverage achieved

### 🚧 Next Implementation: INFRA-005 Exception Handling Framework

Location: `src\DatabaseAutomationPlatform.Infrastructure\Exceptions\`

Required Components:
1. **Custom Exception Hierarchy**
   - `DatabaseAutomationException` - Base exception
   - `DatabaseConnectionException` - Connection failures
   - `StoredProcedureException` - SP execution errors
   - `ValidationException` - Input validation failures
   - `SecurityException` - Security violations
   - `ConfigurationException` - Configuration errors
2. **Global Exception Handler**
   - Middleware to catch all unhandled exceptions
   - Consistent error response format
   - Security-aware error messages
   - Correlation ID tracking

3. **Error Response Models**
   - `ErrorResponse` - Standard error format
   - `ValidationErrorResponse` - Field-level errors
   - `ProblemDetails` - RFC 7807 compliance

## Complete Project Structure

```
D:\dev2\sqlmcp\complete\
├── src\
│   └── DatabaseAutomationPlatform.Infrastructure\
│       ├── Configuration\
│       │   ├── DatabaseOptions.cs ✅
│       │   ├── LoggingOptions.cs ✅
│       │   └── SecurityOptions.cs ✅
│       ├── Data\
│       │   ├── Parameters\
│       │   │   └── StoredProcedureParameter.cs ✅
│       │   ├── Results\
│       │   │   └── StoredProcedureResult.cs ✅
│       │   ├── Metadata\
│       │   │   └── StoredProcedureMetadata.cs ✅
│       │   ├── Security\
│       │   │   └── ParameterSanitizer.cs ✅
│       │   ├── IDbConnectionFactory.cs ✅
│       │   ├── SqlConnectionFactory.cs ✅
│       │   ├── IStoredProcedureExecutor.cs ✅
│       │   └── StoredProcedureExecutor.cs ✅
│       ├── Logging\
│       │   ├── LoggingConfiguration.cs ✅
│       │   ├── ISecurityLogger.cs ✅
│       │   ├── SecurityLogger.cs ✅
│       │   ├── LoggingMiddleware.cs ✅
│       │   ├── CorrelationIdEnricher.cs ✅
│       │   └── AuditEvent.cs ✅
│       ├── Exceptions\ 🚧 (NEXT TO IMPLEMENT)
│       │   ├── DatabaseAutomationException.cs
│       │   ├── DatabaseConnectionException.cs
│       │   ├── StoredProcedureException.cs
│       │   ├── ValidationException.cs
│       │   ├── SecurityException.cs
│       │   ├── ConfigurationException.cs
│       │   └── ExceptionHandlingMiddleware.cs
│       └── Security\ (AFTER EXCEPTIONS)
│           ├── IPermissionService.cs
│           ├── PermissionService.cs
│           ├── IAuditService.cs
│           └── AuditService.cs├── tests\
│   ├── unit\
│   │   └── Infrastructure\
│   │       ├── Configuration\
│   │       │   └── (various test files) ✅
│   │       ├── Data\
│   │       │   ├── Parameters\
│   │       │   │   └── StoredProcedureParameterTests.cs ✅
│   │       │   ├── Security\
│   │       │   │   └── ParameterSanitizerTests.cs ✅
│   │       │   ├── SqlConnectionFactoryTests.cs ✅
│   │       │   └── StoredProcedureExecutorTests.cs ✅
│   │       └── Logging\
│   │           └── (various test files) ✅
│   └── integration\
│       └── Infrastructure\
│           └── Data\
│               ├── StoredProcedureExecutorIntegrationTests.cs ✅
│               └── DatabaseTestFixture.cs ✅
└── docs\
    ├── REQUIREMENTS.md ✅
    ├── README.md ✅
    ├── TODO.md ✅
    ├── FUTURE.md ✅
    ├── IMPLEMENTATION_PLAN.md ✅
    ├── NEXT_SESSION_CONTEXT.md ✅
    ├── INFRA-004-COMPLETION-SUMMARY.md ✅
    └── COMPLETE_PROJECT_CONTEXT.md ✅ (this file)
```

## Critical Implementation Details

### 1. Stored Procedure Executor Patterns

```csharp
// ALWAYS use this pattern for retry logic
_retryPolicy = Policy
    .Handle<SqlException>(IsTransientError)
    .WaitAndRetryAsync(3, 
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
            + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)));
```
### 2. Security Patterns

```csharp
// ALWAYS sanitize parameters before execution
var sanitizationResult = _parameterSanitizer.ValidateParameters(procedureName, parameters);
if (!sanitizationResult.IsValid)
{
    _securityLogger.LogSecurityEvent("ParameterValidationFailed", 
        new { ProcedureName = procedureName, Errors = sanitizationResult.Errors });
    return StoredProcedureResult<T>.Failure(error, procedureName, elapsedMs);
}
```

### 3. Audit Logging Pattern

```csharp
// ALWAYS log audit events for database operations
await LogAuditEventAsync(new AuditEvent
{
    EventType = "StoredProcedureExecuted",
    ProcedureName = procedureName,
    ExecutionId = executionId,
    Success = true,
    RowsAffected = result.RowsAffected,
    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
    RetryCount = retryCount,
    Parameters = GetParametersForAudit(paramList)
});
```

### 4. Safe Error Messages

```csharp
// NEVER expose internal details
private string GetSafeErrorMessage(Exception ex)
{
    return ex switch
    {
        SqlException sqlEx when IsTransientError(sqlEx) => 
            "A temporary database error occurred. Please try again.",
        SqlException sqlEx when sqlEx.Number == 2812 => 
            "The requested stored procedure does not exist.",
        TimeoutException => 
            "The operation timed out. Please try again or contact support.",
        _ => "An error occurred while executing the database operation."
    };
}
```
## SQL Server Transient Error Codes

Always retry these error codes:
- 1205: Deadlock victim
- 1222: Lock request timeout
- 49918: Cannot process request. Not enough resources
- 49919: Cannot process create or update request
- 49920: Cannot process request. Too many operations
- 4060: Cannot open database
- 40143: Connection terminated
- 40613: Database unavailable
- 40501: Service busy
- 40540: Service temporarily unavailable
- 233: Connection initialization error
- 64: Connection failed
- 20: Instance not found
- -2: Timeout expired

## Testing Requirements

### Unit Tests Must Include:
1. **Happy Path**: Normal execution scenarios
2. **Error Handling**: All exception types
3. **Retry Logic**: Transient error simulation
4. **Circuit Breaker**: Open/closed states
5. **Parameter Validation**: SQL injection attempts
6. **Resource Disposal**: Verify using/dispose patterns
7. **Cancellation**: CancellationToken handling
8. **Logging Verification**: All log calls verified
9. **Security Events**: Audit trail verification
10. **Edge Cases**: Null, empty, boundary values

### Integration Tests Must Include:
1. Real database connections
2. Actual stored procedure execution
3. Transaction commit/rollback
4. Timeout scenarios
5. Concurrent execution
6. Large result sets
7. Multiple result sets

## IMMEDIATE NEXT STEPS

### 1. Start Exception Handling Framework (INFRA-005)
```bash
# Create directories
mkdir D:\dev2\sqlmcp\complete\src\DatabaseAutomationPlatform.Infrastructure\Exceptions
mkdir D:\dev2\sqlmcp\complete\src\DatabaseAutomationPlatform.Infrastructure\Models
mkdir D:\dev2\sqlmcp\complete\tests\unit\Infrastructure\Exceptions
```
### 2. Exception Hierarchy Design
- Base: `DatabaseAutomationException`
- Connection: `DatabaseConnectionException`
- Execution: `StoredProcedureException`
- Validation: `ValidationException`
- Security: `SecurityException`
- Configuration: `ConfigurationException`

### 3. Implementation Order
1. Create base exception class with standard properties
2. Create specific exception types
3. Implement global exception handler middleware
4. Create error response models
5. Add comprehensive unit tests
6. Update existing code to use new exceptions

## Critical Reminders

### ALWAYS:
- Use sequential thinking tool for planning
- Write complete code listings
- Include comprehensive error handling
- Add security logging for suspicious activities
- Validate and sanitize all inputs
- Write tests FIRST (TDD approach)
- Maintain 95%+ code coverage
- Use async/await throughout
- Follow SOLID principles
- Document all public APIs

### NEVER:
- Use dynamic SQL directly
- Expose internal error details
- Trust user input
- Skip parameter validation
- Ignore security implications
- Write partial code snippets
- Use semicolons in T-SQL
- Create circular dependencies
- Skip error handling
- Compromise on test coverage

## Performance Targets
- Connection pool: 100-200 connections
- Query timeout: 30 seconds default
- Retry attempts: 3 with exponential backoff
- Circuit breaker: Open after 5 failures
- Cache duration: 5 minutes for metadata
- Response time: <1 second for 95% of requests

## Security Checklist
- [ ] All parameters sanitized
- [ ] Audit trail for every operation
- [ ] Security events logged
- [ ] Safe error messages
- [ ] Principle of least privilege
- [ ] No dynamic SQL execution
- [ ] Input validation on all methods
- [ ] Output encoding where needed
## Session Start Checklist
1. Read this entire document first
2. Review NEXT_SESSION_CONTEXT.md for immediate tasks
3. Check TODO.md for current priorities
4. Use sequential thinking tool to plan approach
5. Create any missing directories
6. Start with TDD - write tests first

## How to Use This Context

When starting the next session, use this prompt:

---

**PROMPT FOR NEXT SESSION:**

I need to continue working on the Database Automation Platform (SQL MCP) project. Please read the complete context document at `D:\dev2\sqlmcp\complete\docs\COMPLETE_PROJECT_CONTEXT.md` first.

This is an ENTERPRISE-GRADE system with strict security and quality requirements. We just completed INFRA-004 (Stored Procedure Executor) with 95%+ test coverage.

**CRITICAL REQUIREMENTS:**
- Security is paramount - SQL injection prevention, audit logging, safe errors
- Minimum 95% test coverage on all code
- Use sequential thinking tool for planning
- Complete code listings only (no partial snippets)
- T-SQL without semicolons
- Comprehensive error handling and logging

**NEXT TASK**: Implement INFRA-005 (Exception Handling Framework) starting with:
1. Custom exception hierarchy
2. Global exception handler middleware
3. Error response models
4. Comprehensive unit tests

Please start by:
1. Using sequential thinking to plan the exception framework
2. Creating the necessary directories
3. Implementing the base exception class with TDD approach

Remember: This is production code for an enterprise system. Security, reliability, and maintainability are non-negotiable.

---

## Final Notes

This system will eventually handle:
- Millions of database operations per day
- Concurrent access from multiple AI agents
- Sensitive financial and customer data
- Regulatory compliance requirements
- 99.99% uptime SLA

Every decision must consider these scale and security requirements.

**Project Mantra**: "Secure by Design, Reliable by Default, Scalable by Architecture"

END OF CONTEXT DOCUMENT