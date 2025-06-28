# Database Automation Platform - Demo Scenarios

This directory contains demonstration scenarios showcasing the capabilities of the Database Automation Platform using the Model Context Protocol (MCP).

## Prerequisites

- Database Automation Platform API running locally or accessible endpoint
- Valid authentication credentials (JWT token or API key)
- SQL Server database with appropriate permissions
- HTTP client (curl, Postman, or any programming language HTTP library)

## Available Demos

### 1. Developer Scenarios
- **01-query-execution**: Basic query execution and result handling
- **02-sql-generation**: AI-assisted SQL query generation
- **03-query-optimization**: Query performance optimization

### 2. DBA Scenarios
- **04-health-monitoring**: Database health monitoring and alerting
- **05-performance-analysis**: Query performance analysis and tuning
- **06-backup-restore**: Automated backup and restore operations

### 3. Schema Management
- **07-schema-documentation**: Automatic schema documentation generation
- **08-schema-comparison**: Compare schemas between databases
- **09-migration-generation**: Generate migration scripts

### 4. Analytics Scenarios
- **10-data-profiling**: Profile data quality and statistics
- **11-anomaly-detection**: Detect anomalies in data
- **12-pattern-detection**: Identify patterns in data

## Getting Started

1. **Obtain Authentication Token**
   ```bash
   # Using curl
   curl -X POST http://localhost:5000/api/v1/auth/token \
     -H "Content-Type: application/json" \
     -d '{"username": "demo", "password": "demo123"}'
   ```

2. **Set Environment Variables**
   ```bash
   export API_URL="http://localhost:5000"
   export AUTH_TOKEN="your-jwt-token-here"
   ```

3. **Run Demo Scripts**
   Each demo directory contains:
   - `scenario.md` - Detailed description of the scenario
   - `request.json` - Sample MCP request
   - `run.sh` - Bash script to execute the demo
   - `expected-response.json` - Expected response format

## MCP Protocol Basics

All interactions follow the MCP protocol format:

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 1,
  "params": {
    "name": "tool-name",
    "arguments": {
      "param1": "value1",
      "param2": "value2"
    }
  }
}
```

## Available Tools

1. **query** - Execute read-only SQL queries
2. **execute** - Execute SQL commands (INSERT, UPDATE, DELETE)
3. **schema** - Get schema information
4. **analyze** - Analyze performance or data patterns

## Quick Examples

### Execute a Query
```bash
curl -X POST $API_URL/api/v1/mcp \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "id": 1,
    "params": {
      "name": "query",
      "arguments": {
        "database": "AdventureWorks",
        "query": "SELECT TOP 10 * FROM Sales.Customer"
      }
    }
  }'
```

### Get Schema Information
```bash
curl -X POST $API_URL/api/v1/mcp \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "id": 2,
    "params": {
      "name": "schema",
      "arguments": {
        "database": "AdventureWorks",
        "objectType": "Table",
        "objectName": "Sales.Customer"
      }
    }
  }'
```

## Error Handling

The platform returns structured error responses:
```json
{
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": {
      "details": "Specific error details"
    }
  }
}
```

## Best Practices

1. Always include proper authentication headers
2. Use appropriate timeouts for long-running queries
3. Handle errors gracefully in your applications
4. Use transactions for data modification operations
5. Monitor rate limits to avoid throttling

## Support

For questions or issues:
- Check the API documentation at `/swagger`
- Review integration tests for usage examples
- Contact the Database Automation Team