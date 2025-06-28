# SQL MCP Audit Logging System

## Overview
The SQL MCP Audit Logging System provides comprehensive, enterprise-scale audit logging capabilities for tracking all database operations, security events, and system activities. Built with performance, security, and compliance in mind, it supports millions of operations per day with minimal overhead.

## Features

### 🔍 Comprehensive Event Tracking
- Database operations (CRUD) with before/after values
- Security events (authentication, authorization)
- System events (startup, configuration changes)
- Performance metrics and resource usage

### 🔒 Security & Compliance
- Tamper-proof storage with hash chaining
- GDPR compliance with PII handling
- Support for SOC 2, PCI DSS, and HIPAA requirements
- Automatic data classification and masking

### ⚡ High Performance
- Sub-millisecond overhead for standard operations
- Asynchronous, non-blocking audit writes
- In-memory buffering with background processing
- Support for 1M+ events per minute

### 🔧 Flexible Configuration
- Runtime-configurable audit levels
- Per-entity and per-operation granularity
- Performance sampling for high-volume operations
- Circuit breaker pattern for resilience

## Architecture

### Components
1. **Audit Events**: Strongly-typed event models with inheritance hierarchy
2. **Interceptors**: Transparent interception of database operations
3. **Repositories**: Secure storage with SQL and Azure Table Storage
4. **Services**: Orchestration, buffering, and background processing
5. **Configuration**: Dynamic configuration with hot-reload support

### Integration Points
- **Correlation Tracking**: Integrates with exception handling framework
- **Structured Logging**: Full Serilog integration
- **Azure Services**: Key Vault, Table Storage, Application Insights
- **Stored Procedures**: Seamless SP execution interception

## Getting Started

### Installation
```bash
# Install NuGet packages
dotnet add package Serilog.AspNetCore
dotnet add package Microsoft.Azure.Cosmos.Table
dotnet add package Polly
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

### Basic Configuration
```csharp
// In Program.cs
builder.Services.AddAuditing(options =>
{
    options.Level = AuditLevel.Detailed;
    options.BufferSize = 1000;
    options.FlushInterval = TimeSpan.FromSeconds(5);
});

// In appsettings.json
{
  "Auditing": {
    "Enabled": true,
    "Level": "Detailed",
    "RetentionDays": 90,
    "StorageConnection": "UseDevelopmentStorage=true"
  }
}
```

### Usage Examples

#### Automatic Database Operation Auditing
```csharp
// Automatically captured by interceptor
var result = await _repository.ExecuteStoredProcedureAsync(
    "sp_UpdateUser",
    new { UserId = 123, Email = "new@example.com" }
);
```

#### Manual Security Event
```csharp
await _auditService.LogSecurityEventAsync(
    SecurityEventType.UnauthorizedAccess,
    "Attempted access to restricted resource",
    new { Resource = "/api/admin", UserId = userId }
);
```

#### Custom Business Event
```csharp
await _auditService.LogEventAsync(new CustomAuditEvent
{
    EventType = "OrderProcessed",
    EntityId = orderId,
    Details = new { Amount = 123.45, Currency = "USD" }
});
```

## Development

### Project Structure
```
src/Core/Auditing/
├── Attributes/          # Data classification attributes
├── Configuration/       # Audit configuration models
├── Interceptors/        # Database operation interceptors
├── Interfaces/          # Core contracts
├── Models/              # Audit event models
└── Services/            # Audit services

src/Infrastructure/Auditing/
└── Repositories/        # Storage implementations

tests/
├── Unit/Core/Auditing/  # Unit tests
└── Integration/Auditing/ # Integration tests
```

### Testing
```bash
# Run all audit tests
dotnet test --filter "FullyQualifiedName~Auditing"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~Auditing"
```

### Performance Testing
```bash
# Run load tests
dotnet run --project tests/Performance/AuditLoadTests
```

## Configuration Reference

### Audit Levels
- `None`: No auditing
- `Critical`: Security events only
- `Basic`: CRUD operations without data
- `Detailed`: CRUD with before/after values
- `Verbose`: All operations including reads

### Storage Options
- **SQL Server**: Primary storage with partitioning
- **Azure Table Storage**: Long-term archival
- **Event Hub**: Real-time streaming (optional)

### GDPR Features
- Automatic PII detection and masking
- Right-to-erasure support
- Data retention policies
- Audit access logging

## Monitoring

### Key Metrics
- Audit events per second
- Average write latency
- Buffer utilization
- Failed audit writes

### Health Checks
```csharp
app.MapHealthChecks("/health/audit", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("audit")
});
```

## Troubleshooting

### Common Issues
1. **High latency**: Check buffer settings and flush interval
2. **Missing events**: Verify audit level configuration
3. **Storage errors**: Check connection strings and permissions
4. **Memory usage**: Adjust buffer size and flush frequency

### Debug Logging
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "SqlMcp.Core.Auditing": "Debug"
      }
    }
  }
}
```

## Contributing
See [CONTRIBUTING.md](../CONTRIBUTING.md) for development guidelines.

## License
Copyright (c) 2025 SQL MCP Project. All rights reserved.
