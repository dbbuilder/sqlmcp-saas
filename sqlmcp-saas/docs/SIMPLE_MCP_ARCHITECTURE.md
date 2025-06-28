# Simple MCP Server Architecture

## Overview

The Simple MCP Server provides basic SQL Server connectivity for AI models using the Model Context Protocol. This is our MVP that allows users to:

1. Connect their SQL Server database
2. Expose schema information (tables, views, columns)
3. Execute safe read-only queries
4. Return results in AI-friendly formats

## Core Components

### 1. MCP Server Core (`/src/mcp-server/`)

```typescript
interface SimpleMcpServer {
  // Connection management
  connect(connectionString: string): Promise<void>
  disconnect(): Promise<void>
  testConnection(): Promise<boolean>
  
  // Schema discovery
  getTables(): Promise<Table[]>
  getViews(): Promise<View[]>
  getColumns(tableName: string): Promise<Column[]>
  
  // Query execution
  executeQuery(query: string): Promise<QueryResult>
  explainQuery(query: string): Promise<string>
}
```

### 2. Security Layer

- **Connection String Encryption**: Using Azure Key Vault
- **Query Validation**: 
  - Only SELECT statements allowed
  - No system tables access
  - Query timeout limits
  - Row limit enforcement (based on tier)
- **API Key Authentication**: Per-user MCP endpoints
- **Audit Logging**: All queries logged with user context

### 3. MCP Protocol Implementation

```typescript
// MCP Tool Definitions
const tools = [
  {
    name: "list_tables",
    description: "List all available tables in the database",
    inputSchema: {}
  },
  {
    name: "describe_table",
    description: "Get schema information for a specific table",
    inputSchema: {
      type: "object",
      properties: {
        tableName: { type: "string" }
      }
    }
  },
  {
    name: "query_data",
    description: "Execute a SELECT query on the database",
    inputSchema: {
      type: "object",
      properties: {
        query: { type: "string" },
        limit: { type: "number", default: 100 }
      }
    }
  }
]
```

### 4. User Management Integration

- Each user gets a unique MCP endpoint URL
- Connection strings stored encrypted per user
- Usage tracking (queries/month)
- Tier-based limits enforcement

## Technical Stack

- **Runtime**: Node.js with TypeScript
- **Database**: SQL Server connectivity via `mssql` package
- **MCP SDK**: Official MCP SDK for protocol compliance
- **Security**: 
  - bcrypt for password hashing
  - JWT for API authentication
  - Azure Key Vault for secrets
- **Monitoring**: Application Insights

## Deployment Architecture

```
User -> Claude/ChatGPT -> MCP Endpoint -> API Gateway -> MCP Server -> SQL Server
                                               |
                                          Auth Service
                                               |
                                          Usage Tracking
```

## MVP Features

1. **Basic Operations**:
   - List tables and views
   - Describe table schemas
   - Execute simple SELECT queries
   - Natural language to SQL translation hints

2. **Security**:
   - Read-only access
   - Query validation
   - Connection isolation
   - Rate limiting

3. **User Experience**:
   - Simple connection wizard
   - Test connection feature
   - Usage dashboard
   - Query history

## Future Enhancements (Pro/Enterprise)

- Complex query builder
- Data analysis tools
- Write operations (with approval workflow)
- Custom stored procedure execution
- Advanced caching
- Query optimization suggestions

## Implementation Priority

1. Core MCP protocol implementation
2. SQL Server connection handling
3. Basic security layer
4. Schema discovery tools
5. Query execution with limits
6. User dashboard
7. Usage tracking
8. Documentation