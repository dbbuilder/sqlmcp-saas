# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Database Automation Platform - an enterprise-grade platform that enables AI assistants to safely perform SQL database operations through the Model Context Protocol (MCP). The platform provides SQL development, DBA, schema management, and analytics capabilities with comprehensive security and compliance features.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test tests/unit/DatabaseAutomationPlatform.Tests.Unit.csproj
dotnet test tests/integration/DatabaseAutomationPlatform.Tests.Integration.csproj

# Restore packages
dotnet restore
```

## High-Level Architecture

The platform follows Clean Architecture principles with Domain-Driven Design:

1. **Domain Layer** (`DatabaseAutomationPlatform.Domain/`) - Core business entities and interfaces, no dependencies
2. **Application Layer** (`DatabaseAutomationPlatform.Application/`) - Business logic, use cases, and application services
3. **Infrastructure Layer** (`DatabaseAutomationPlatform.Infrastructure/`) - External service implementations (database, logging, security)
4. **API Layer** (`DatabaseAutomationPlatform.Api/`) - MCP API endpoints and middleware
5. **Feature Modules** - Specialized implementations for Developer, DBA, Schema, and Analytics operations

### Key Architectural Patterns

- **Repository Pattern with Unit of Work** - All database access through repositories
- **Stored Procedures Only** - No direct SQL queries; all database operations via stored procedures using `IStoredProcedureExecutor`
- **Dependency Injection** - Constructor injection throughout, configured in Program.cs
- **Options Pattern** - All configuration through strongly-typed options classes
- **Middleware Pipeline** - Cross-cutting concerns (auth, logging, error handling) as middleware
- **Correlation ID Tracking** - All operations tracked with correlation IDs for distributed tracing

### Security Architecture

- **Azure Key Vault Integration** - All secrets stored in Key Vault, never in config files
- **Encryption at Rest and in Transit** - Using Azure SQL TDE and TLS 1.2+
- **Comprehensive Audit Logging** - Every operation logged with who/what/when/where
- **Parameter Sanitization** - Built into `StoredProcedureExecutor` to prevent SQL injection
- **Error Message Sanitization** - Custom exception hierarchy ensures no sensitive data in error responses

### Database Operations

**IMPORTANT**: Always set the database name explicitly in SQL commands:
```csharp
// Example: Always specify database context
var parameters = new[]
{
    new SqlParameter("@DatabaseName", databaseName),
    new SqlParameter("@TableName", tableName)
};
await _executor.ExecuteAsync("sp_GetTableInfo", parameters);
```

### Testing Strategy

- **Unit Tests** - Test individual components in isolation (95%+ coverage target)
- **Integration Tests** - Test database operations with TestContainers
- **Performance Tests** - Validate response times and resource usage
- **Security Tests** - Explicit tests for authentication, authorization, and data protection

Use FluentAssertions for all test assertions for better readability.

## Development Guidelines

1. **Never expose sensitive information** in logs, error messages, or responses
2. **Always use parameterized stored procedures** - no string concatenation for SQL
3. **Include correlation IDs** in all log entries and error responses
4. **Write tests first** (TDD) - especially for security scenarios
5. **Use structured logging** with Serilog - include operation context
6. **Follow Microsoft C# conventions** - use analyzers to enforce standards
7. **Document public APIs** with XML comments for IntelliSense

## Current Implementation Status

### Completed Components
- Infrastructure layer foundation (database factory, logging, stored procedure executor)
- Exception handling framework with security-aware error responses
- Core project structure following Clean Architecture

### Next Implementation Tasks
1. Audit logging system (INFRA-006)
2. Azure Key Vault configuration provider (CONFIG-001)
3. MCP API implementation (API-001, API-002)
4. Core database schema and stored procedures (DB-001)

## Environment Setup

1. Install .NET 8.0 SDK
2. Set up local SQL Server or Azure SQL Database
3. Configure user secrets for development:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
   ```
4. Copy `appsettings.example.json` to `appsettings.Development.json` and update settings

## Important Notes

- The solution file includes projects that don't have .csproj files yet - these are planned for future implementation
- All database operations must go through stored procedures defined in `/database/stored-procedures/`
- Security and compliance are non-negotiable - every feature must consider security implications
- Use Application Insights for monitoring in production environments