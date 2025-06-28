# PROMPT FOR NEXT SESSION: SQL MCP Database Automation Platform

## Context Setup
I need to continue working on the Database Automation Platform (SQL MCP) project. Please read the complete context document at `D:\dev2\sqlmcp\complete\docs\COMPLETE_PROJECT_CONTEXT.md` first. This is an ENTERPRISE-GRADE system with strict security and quality requirements for AI agents to safely interact with databases.

## Current State
We just completed **INFRA-005 (Exception Handling Framework)** with:
- Custom exception hierarchy (BaseException, ValidationException, DatabaseException, SecurityException, etc.)
- Global exception middleware with security-focused error sanitization
- Error response models (ErrorResponse, ProblemDetails RFC 7807)
- 95%+ test coverage using TDD approach
- Complete security measures (no stack traces, sanitized messages, correlation IDs)

## Project Standards (NON-NEGOTIABLE)
- **Security First**: SQL injection prevention, audit logging, safe error messages, no information disclosure
- **Test Coverage**: Minimum 95% on all code, TDD approach mandatory
- **Enterprise Scale**: Handle millions of operations/day, concurrent AI agents, 99.99% uptime
- **Code Quality**: Complete listings only (no snippets), comprehensive error handling, detailed logging

## Technical Requirements
- **Backend**: C# .NET Core, Entity Framework Core (stored procedures only), Azure deployment
- **Configuration**: appsettings.json, Azure Key Vault for secrets
- **Patterns**: Repository pattern, dependency injection, SOLID principles
- **Database**: T-SQL without semicolons, parameterized queries only, print dynamic SQL
- **Testing**: xUnit, FluentAssertions, Moq, TDD approach

## Next Task: INFRA-006 (Audit Logging System)
Design and implement a comprehensive audit logging system that tracks:
1. **All database operations** (who, what, when, where, correlation ID)
2. **Security events** (authentication, authorization, suspicious activities)
3. **Data changes** (before/after values for sensitive operations)
4. **Performance metrics** (query execution times, resource usage)

Key requirements:
- Integrate with existing exception handling (use correlation IDs)
- Async, non-blocking audit writes
- Configurable audit levels (Verbose, Normal, Security-Only)
- Tamper-proof audit trail (write-once)
- GDPR compliance (PII handling, retention policies)

## Approach Instructions
1. **Start with sequential thinking tool** to plan the audit system architecture
2. **Create directory structure** for audit components
3. **Write comprehensive tests first** (TDD):
   - Audit event models tests
   - Audit interceptor tests
   - Audit storage tests
   - Integration tests
4. **Implement incrementally** with full code listings
5. **Maintain correlation ID flow** through entire system

## Context Files to Review
- `D:\dev2\sqlmcp\complete\docs\TODO.md` - Current task list
- `D:\dev2\sqlmcp\complete\docs\COMPLETE_PROJECT_CONTEXT.md` - Full project context
- Recent implementations in `D:\dev2\sqlmcp\complete\src\Core\Exceptions\` - Exception framework
- `D:\dev2\sqlmcp\complete\src\Core\ErrorHandling\GlobalExceptionMiddleware.cs` - Middleware pattern

## Critical Reminders
- This system will handle **financial data** and **PII**
- Every design decision must consider **security**, **reliability**, and **auditability**
- Use sequential thinking tool for complex planning
- Complete code listings only (no partial snippets)
- Test coverage reports after each component
- Think like a **principal engineer** designing for enterprise scale

## Question to Start
"I'm ready to implement INFRA-006 (Audit Logging System) for the SQL MCP project. Should I begin with the sequential thinking tool to architect the audit system, considering integration with our exception handling framework and correlation ID tracking?"

---
**Project Mantra**: "Secure by Design, Reliable by Default, Scalable by Architecture"
