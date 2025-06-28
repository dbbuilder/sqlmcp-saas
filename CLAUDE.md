# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This repository contains three SQL Server MCP (Model Context Protocol) integration projects:

1. **POC** (`/POC/`) - Containerized proof-of-concept translating natural language to T-SQL
2. **Complete** (`/complete/`) - Enterprise-grade Database Automation Platform with comprehensive MCP API
3. **RemoteMCP** (`/RemoteMCP/`) - Azure Functions-based MCP server implementation

## Essential Development Commands

```bash
# Build all projects
dotnet build

# Run tests
dotnet test                                      # All tests
dotnet test --filter "Category=Unit"            # Unit tests only
dotnet test --filter "Category=Integration"     # Integration tests only

# Run projects
dotnet run --project POC/src/SqlMcpPoc.ConsoleApp
dotnet run --project complete/src/DatabaseAutomationPlatform.Api
dotnet run --project RemoteMCP/src/SqlServerMcpFunctions

# Docker (POC only)
docker build -t sqlmcp-poc -f POC/docker/Dockerfile POC/
docker run --rm -it -v "$(pwd)/POC/config/config.json:/app/config.json:ro" sqlmcp-poc "query"
```

## Architecture Overview

All projects follow Clean Architecture with these layers:

- **Domain**: Core entities, interfaces, value objects (no dependencies)
- **Application**: Business logic, service interfaces, DTOs
- **Infrastructure**: Data access, external services, security implementations
- **API/Functions**: HTTP endpoints, MCP protocol handlers

Key architectural decisions:
- **Stored Procedures Only**: All database operations must use stored procedures
- **Repository + Unit of Work**: Data access patterns for testability
- **Dependency Injection**: All services registered via DI container
- **Configuration**: Azure Key Vault for secrets, appsettings.json for non-sensitive config

## Database Conventions

- Always specify database name in SQL commands: `sqlcmd -d DatabaseName`
- All stored procedures follow naming: `sp_[Module]_[Action]` (e.g., `sp_Schema_Compare`)
- Audit logging required for all data modifications
- Parameter sanitization mandatory for SQL injection prevention

## Testing Strategy

- **Unit Tests**: Mock all dependencies, test business logic in isolation
- **Integration Tests**: Use `DatabaseTestFixture` for database setup/teardown
- **Test Naming**: `MethodName_Scenario_ExpectedResult`
- **Coverage Target**: 95% for critical paths

## Security Requirements

- API Key authentication via `X-API-Key` header
- Azure Key Vault for all connection strings and secrets
- Comprehensive audit logging with correlation IDs
- Parameter sanitization using `ParameterSanitizer` class
- No dynamic SQL - stored procedures only

## MCP Implementation Notes

The MCP (Model Context Protocol) implementation in `/complete/src/DatabaseAutomationPlatform.Api/Controllers/McpController.cs` handles:
- Tool registration and discovery
- Resource management
- Secure execution of SQL operations
- Result formatting for LLM consumption

## Common Development Tasks

When implementing new features:
1. Start with domain entities/interfaces
2. Implement repository interfaces in Infrastructure
3. Create application services
4. Add API endpoints with proper validation
5. Write unit tests first, then integration tests
6. Update stored procedures in `/complete/database/stored-procedures/`

## Configuration Files

- `appsettings.json`: Non-sensitive configuration
- `appsettings.Development.json`: Local development overrides
- `local.settings.json`: Azure Functions local settings
- Environment variables override all JSON settings