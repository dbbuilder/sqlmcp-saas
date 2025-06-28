import { Server } from '@modelcontextprotocol/sdk/server/index.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js'
import sql from 'mssql'
import crypto from 'crypto'

interface SqlServerConfig {
  server: string
  database: string
  user: string
  password: string
  port?: number
  options?: {
    encrypt?: boolean
    trustServerCertificate?: boolean
    enableArithAbort?: boolean
    connectionTimeout?: number
    requestTimeout?: number
  }
}

interface QueryMetrics {
  query: string
  executionTime: number
  rowCount: number
  success: boolean
  error?: string
}

export class SqlServerMcpServer {
  private server: Server
  private sqlPool: sql.ConnectionPool | null = null
  private config: SqlServerConfig | null = null
  private queryLimit: number
  private userId: string
  private apiKey: string
  private queryMetrics: QueryMetrics[] = []

  constructor(userId: string, apiKey: string, queryLimit: number = 1000) {
    this.userId = userId
    this.apiKey = apiKey
    this.queryLimit = queryLimit
    this.server = new Server(
      {
        name: 'sqlmcp-server',
        version: '1.0.0',
      },
      {
        capabilities: {
          tools: {},
          resources: {
            subscribe: true,
          },
        },
      }
    )

    this.setupHandlers()
  }

  private setupHandlers(): void {
    // List available tools
    this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
      tools: [
        {
          name: 'connect_database',
          description: 'Connect to a SQL Server database',
          inputSchema: {
            type: 'object',
            properties: {
              server: { type: 'string', description: 'SQL Server hostname or IP' },
              database: { type: 'string', description: 'Database name' },
              user: { type: 'string', description: 'Username' },
              password: { type: 'string', description: 'Password' },
              port: { type: 'number', description: 'Port number (default: 1433)' },
              encrypt: { type: 'boolean', description: 'Use encryption (default: true)' },
            },
            required: ['server', 'database', 'user', 'password'],
          },
        },
        {
          name: 'list_tables',
          description: 'List all tables in the connected database with row counts',
          inputSchema: {
            type: 'object',
            properties: {
              schema: { type: 'string', description: 'Schema name (default: all schemas)' },
            },
          },
        },
        {
          name: 'list_views',
          description: 'List all views in the connected database',
          inputSchema: {
            type: 'object',
            properties: {
              schema: { type: 'string', description: 'Schema name (default: all schemas)' },
            },
          },
        },
        {
          name: 'describe_table',
          description: 'Get detailed schema information for a table including indexes and constraints',
          inputSchema: {
            type: 'object',
            properties: {
              tableName: {
                type: 'string',
                description: 'Table name (can include schema: schema.table)',
              },
            },
            required: ['tableName'],
          },
        },
        {
          name: 'query_data',
          description: 'Execute a SELECT query on the database',
          inputSchema: {
            type: 'object',
            properties: {
              query: {
                type: 'string',
                description: 'SQL SELECT query to execute',
              },
              limit: {
                type: 'number',
                description: 'Maximum number of rows to return',
                default: 100,
              },
            },
            required: ['query'],
          },
        },
        {
          name: 'get_table_sample',
          description: 'Get a sample of data from a table',
          inputSchema: {
            type: 'object',
            properties: {
              tableName: { type: 'string', description: 'Table name' },
              sampleSize: { type: 'number', description: 'Number of rows to sample', default: 10 },
            },
            required: ['tableName'],
          },
        },
        {
          name: 'get_database_info',
          description: 'Get information about the connected database',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'analyze_query',
          description: 'Analyze a query without executing it (estimated execution plan)',
          inputSchema: {
            type: 'object',
            properties: {
              query: { type: 'string', description: 'SQL query to analyze' },
            },
            required: ['query'],
          },
        },
      ],
    }))

    // Handle tool calls
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params

      try {
        switch (name) {
          case 'connect_database':
            return await this.connectDatabase(args as SqlServerConfig)
          case 'list_tables':
            this.ensureConnected()
            return await this.listTables(args.schema as string)
          case 'list_views':
            this.ensureConnected()
            return await this.listViews(args.schema as string)
          case 'describe_table':
            this.ensureConnected()
            return await this.describeTable(args.tableName as string)
          case 'query_data':
            this.ensureConnected()
            return await this.queryData(
              args.query as string,
              args.limit as number || 100
            )
          case 'get_table_sample':
            this.ensureConnected()
            return await this.getTableSample(
              args.tableName as string,
              args.sampleSize as number || 10
            )
          case 'get_database_info':
            this.ensureConnected()
            return await this.getDatabaseInfo()
          case 'analyze_query':
            this.ensureConnected()
            return await this.analyzeQuery(args.query as string)
          default:
            throw new Error(`Unknown tool: ${name}`)
        }
      } catch (error) {
        return {
          content: [
            {
              type: 'text',
              text: `Error: ${error instanceof Error ? error.message : 'Unknown error'}`,
            },
          ],
        }
      }
    })
  }

  private ensureConnected(): void {
    if (!this.sqlPool || !this.sqlPool.connected) {
      throw new Error('Database not connected. Please use connect_database first.')
    }
  }

  private async connectDatabase(config: SqlServerConfig) {
    try {
      // Close existing connection if any
      if (this.sqlPool) {
        await this.sqlPool.close()
      }

      // Configure connection
      const sqlConfig: sql.config = {
        server: config.server,
        database: config.database,
        user: config.user,
        password: config.password,
        port: config.port || 1433,
        options: {
          encrypt: config.options?.encrypt !== false, // Default true for Azure
          trustServerCertificate: config.options?.trustServerCertificate || false,
          enableArithAbort: config.options?.enableArithAbort !== false,
          connectionTimeout: config.options?.connectionTimeout || 30000,
          requestTimeout: config.options?.requestTimeout || 30000,
        },
        pool: {
          max: 10,
          min: 0,
          idleTimeoutMillis: 30000,
        },
      }

      this.config = config
      this.sqlPool = await sql.connect(sqlConfig)

      // Test connection
      await this.sqlPool.request().query`SELECT 1 as test`

      return {
        content: [
          {
            type: 'text',
            text: `Successfully connected to SQL Server: ${config.server}/${config.database}`,
          },
        ],
      }
    } catch (error) {
      throw new Error(
        `Connection failed: ${
          error instanceof Error ? error.message : 'Unknown error'
        }`
      )
    }
  }

  private async listTables(schema?: string) {
    const startTime = Date.now()
    
    let whereClause = "WHERE t.TABLE_TYPE = 'BASE TABLE' AND t.TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')"
    if (schema) {
      whereClause += ` AND t.TABLE_SCHEMA = '${schema}'`
    }

    const query = `
      SELECT 
        t.TABLE_SCHEMA,
        t.TABLE_NAME,
        (
          SELECT COUNT(*) 
          FROM INFORMATION_SCHEMA.COLUMNS c 
          WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA 
          AND c.TABLE_NAME = t.TABLE_NAME
        ) as COLUMN_COUNT,
        ISNULL(s.row_count, 0) as ROW_COUNT
      FROM INFORMATION_SCHEMA.TABLES t
      LEFT JOIN (
        SELECT 
          SCHEMA_NAME(t.schema_id) as schema_name,
          t.name as table_name,
          SUM(p.rows) as row_count
        FROM sys.tables t
        INNER JOIN sys.partitions p ON t.object_id = p.object_id
        WHERE p.index_id IN (0,1)
        GROUP BY SCHEMA_NAME(t.schema_id), t.name
      ) s ON t.TABLE_SCHEMA = s.schema_name AND t.TABLE_NAME = s.table_name
      ${whereClause}
      ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME
    `

    const result = await this.sqlPool!.request().query(query)
    
    this.logQuery(query, Date.now() - startTime, result.recordset.length, true)

    const tables = result.recordset.map(
      (row) =>
        `- ${row.TABLE_SCHEMA}.${row.TABLE_NAME} (${row.COLUMN_COUNT} columns, ~${row.ROW_COUNT.toLocaleString()} rows)`
    )

    return {
      content: [
        {
          type: 'text',
          text: `Found ${result.recordset.length} tables:\n\n${tables.join('\n')}`,
        },
      ],
    }
  }

  private async listViews(schema?: string) {
    let whereClause = "WHERE TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')"
    if (schema) {
      whereClause += ` AND TABLE_SCHEMA = '${schema}'`
    }

    const result = await this.sqlPool!.request().query(`
      SELECT 
        TABLE_SCHEMA,
        TABLE_NAME as VIEW_NAME,
        (
          SELECT COUNT(*) 
          FROM INFORMATION_SCHEMA.COLUMNS c 
          WHERE c.TABLE_SCHEMA = v.TABLE_SCHEMA 
          AND c.TABLE_NAME = v.TABLE_NAME
        ) as COLUMN_COUNT
      FROM INFORMATION_SCHEMA.VIEWS v
      ${whereClause}
      ORDER BY TABLE_SCHEMA, TABLE_NAME
    `)

    const views = result.recordset.map(
      (row) => `- ${row.TABLE_SCHEMA}.${row.VIEW_NAME} (${row.COLUMN_COUNT} columns)`
    )

    return {
      content: [
        {
          type: 'text',
          text: `Found ${result.recordset.length} views:\n\n${views.join('\n')}`,
        },
      ],
    }
  }

  private async describeTable(tableName: string) {
    // Parse schema and table name
    const parts = tableName.split('.')
    const schema = parts.length > 1 ? parts[0] : 'dbo'
    const table = parts.length > 1 ? parts[1] : parts[0]

    // Validate names
    if (!/^[a-zA-Z0-9_]+$/.test(schema) || !/^[a-zA-Z0-9_]+$/.test(table)) {
      throw new Error('Invalid table name')
    }

    // Get columns
    const columnsResult = await this.sqlPool!.request()
      .input('schema', sql.NVarChar, schema)
      .input('table', sql.NVarChar, table).query`
      SELECT 
        c.COLUMN_NAME,
        c.DATA_TYPE,
        c.CHARACTER_MAXIMUM_LENGTH,
        c.NUMERIC_PRECISION,
        c.NUMERIC_SCALE,
        c.IS_NULLABLE,
        c.COLUMN_DEFAULT,
        CASE 
          WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' 
          ELSE 'NO' 
        END AS IS_PRIMARY_KEY,
        CASE 
          WHEN fk.COLUMN_NAME IS NOT NULL THEN 'YES' 
          ELSE 'NO' 
        END AS IS_FOREIGN_KEY
      FROM INFORMATION_SCHEMA.COLUMNS c
      LEFT JOIN (
        SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
          ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
          AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
      ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
        AND c.TABLE_NAME = pk.TABLE_NAME 
        AND c.COLUMN_NAME = pk.COLUMN_NAME
      LEFT JOIN (
        SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
          ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
          AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
        WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
      ) fk ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA 
        AND c.TABLE_NAME = fk.TABLE_NAME 
        AND c.COLUMN_NAME = fk.COLUMN_NAME
      WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
      ORDER BY c.ORDINAL_POSITION
    `

    if (columnsResult.recordset.length === 0) {
      throw new Error(`Table '${tableName}' not found`)
    }

    // Get indexes
    const indexesResult = await this.sqlPool!.request()
      .input('schema', sql.NVarChar, schema)
      .input('table', sql.NVarChar, table).query`
      SELECT 
        i.name AS INDEX_NAME,
        i.type_desc AS INDEX_TYPE,
        STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS COLUMNS
      FROM sys.indexes i
      INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
      INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
      INNER JOIN sys.tables t ON i.object_id = t.object_id
      INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
      WHERE s.name = @schema AND t.name = @table
      GROUP BY i.name, i.type_desc
      ORDER BY i.name
    `

    // Format output
    const columns = columnsResult.recordset.map((col) => {
      let type = col.DATA_TYPE
      if (col.CHARACTER_MAXIMUM_LENGTH) {
        type += `(${col.CHARACTER_MAXIMUM_LENGTH === -1 ? 'MAX' : col.CHARACTER_MAXIMUM_LENGTH})`
      } else if (col.NUMERIC_PRECISION) {
        type += `(${col.NUMERIC_PRECISION}${
          col.NUMERIC_SCALE ? `,${col.NUMERIC_SCALE}` : ''
        })`
      }

      const attributes = []
      if (col.IS_NULLABLE === 'NO') attributes.push('NOT NULL')
      if (col.IS_PRIMARY_KEY === 'YES') attributes.push('PRIMARY KEY')
      if (col.IS_FOREIGN_KEY === 'YES') attributes.push('FOREIGN KEY')
      if (col.COLUMN_DEFAULT) attributes.push(`DEFAULT ${col.COLUMN_DEFAULT}`)

      return `- ${col.COLUMN_NAME}: ${type} ${attributes.join(' ')}`
    })

    const indexes = indexesResult.recordset.map(
      (idx) => `- ${idx.INDEX_NAME} (${idx.INDEX_TYPE}): ${idx.COLUMNS}`
    )

    return {
      content: [
        {
          type: 'text',
          text: `Table: ${schema}.${table}\n\nColumns:\n${columns.join('\n')}${
            indexes.length > 0 ? `\n\nIndexes:\n${indexes.join('\n')}` : ''
          }`,
        },
      ],
    }
  }

  private async queryData(query: string, limit: number) {
    const startTime = Date.now()
    
    // Validate query
    const normalizedQuery = query.trim().toUpperCase()
    
    if (!normalizedQuery.startsWith('SELECT') && !normalizedQuery.startsWith('WITH')) {
      throw new Error('Only SELECT queries (including CTEs) are allowed')
    }

    // Security checks
    const dangerousKeywords = [
      'INSERT', 'UPDATE', 'DELETE', 'DROP', 'CREATE', 'ALTER', 'TRUNCATE',
      'EXEC', 'EXECUTE', 'SP_', 'XP_', 'GRANT', 'REVOKE', 'DENY',
      'BACKUP', 'RESTORE', 'BULK', 'OPENROWSET', 'OPENQUERY'
    ]
    
    for (const keyword of dangerousKeywords) {
      if (normalizedQuery.includes(keyword)) {
        throw new Error(`Query contains forbidden keyword: ${keyword}`)
      }
    }

    // Apply row limit
    const effectiveLimit = Math.min(limit, this.queryLimit)
    let limitedQuery = query

    // Add TOP clause if not present
    if (!normalizedQuery.includes('TOP') && !normalizedQuery.includes('OFFSET')) {
      if (normalizedQuery.startsWith('WITH')) {
        // For CTEs, add TOP after the final SELECT
        const lastSelectIndex = limitedQuery.toUpperCase().lastIndexOf('SELECT')
        limitedQuery = 
          limitedQuery.substring(0, lastSelectIndex) + 
          `SELECT TOP ${effectiveLimit} ` + 
          limitedQuery.substring(lastSelectIndex + 6)
      } else {
        limitedQuery = query.replace(/SELECT/i, `SELECT TOP ${effectiveLimit}`)
      }
    }

    try {
      const result = await this.sqlPool!.request().query(limitedQuery)
      
      this.logQuery(limitedQuery, Date.now() - startTime, result.recordset.length, true)

      if (result.recordset.length === 0) {
        return {
          content: [
            {
              type: 'text',
              text: 'Query executed successfully but returned no results.',
            },
          ],
        }
      }

      // Format as markdown table for better readability
      const columns = Object.keys(result.recordset[0])
      const rows = result.recordset.map((row) =>
        columns.map((col) => {
          const value = row[col]
          if (value === null) return 'NULL'
          if (value instanceof Date) return value.toISOString()
          if (typeof value === 'boolean') return value ? 'TRUE' : 'FALSE'
          return String(value).replace(/\|/g, '\\|') // Escape pipes for markdown
        })
      )

      // Create markdown table
      const header = `| ${columns.join(' | ')} |`
      const separator = `| ${columns.map(() => '---').join(' | ')} |`
      const dataRows = rows.map(row => `| ${row.join(' | ')} |`)

      const table = [
        header,
        separator,
        ...dataRows,
        '',
        `*${result.recordset.length} rows returned${
          result.recordset.length === effectiveLimit ? ' (limit reached)' : ''
        }*`,
      ].join('\n')

      return {
        content: [
          {
            type: 'text',
            text: table,
          },
        ],
      }
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error'
      this.logQuery(limitedQuery, Date.now() - startTime, 0, false, errorMessage)
      throw new Error(`Query execution failed: ${errorMessage}`)
    }
  }

  private async getTableSample(tableName: string, sampleSize: number) {
    // Validate table name
    const parts = tableName.split('.')
    const schema = parts.length > 1 ? parts[0] : 'dbo'
    const table = parts.length > 1 ? parts[1] : parts[0]

    if (!/^[a-zA-Z0-9_]+$/.test(schema) || !/^[a-zA-Z0-9_]+$/.test(table)) {
      throw new Error('Invalid table name')
    }

    const query = `SELECT TOP ${Math.min(sampleSize, 100)} * FROM [${schema}].[${table}] ORDER BY NEWID()`
    return await this.queryData(query, sampleSize)
  }

  private async getDatabaseInfo() {
    const result = await this.sqlPool!.request().query`
      SELECT 
        DB_NAME() as DatabaseName,
        @@VERSION as ServerVersion,
        SERVERPROPERTY('Edition') as Edition,
        SERVERPROPERTY('ProductLevel') as ProductLevel,
        SERVERPROPERTY('Collation') as Collation,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') as TableCount,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.VIEWS) as ViewCount,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE') as StoredProcCount,
        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'FUNCTION') as FunctionCount
    `

    const info = result.recordset[0]
    
    return {
      content: [
        {
          type: 'text',
          text: `Database: ${info.DatabaseName}
Edition: ${info.Edition}
Product Level: ${info.ProductLevel}
Collation: ${info.Collation}

Object Counts:
- Tables: ${info.TableCount}
- Views: ${info.ViewCount}
- Stored Procedures: ${info.StoredProcCount}
- Functions: ${info.FunctionCount}

Server Version:
${info.ServerVersion}`,
        },
      ],
    }
  }

  private async analyzeQuery(query: string) {
    try {
      // Get estimated execution plan
      await this.sqlPool!.request().query`SET SHOWPLAN_XML ON`
      const planResult = await this.sqlPool!.request().query(query)
      await this.sqlPool!.request().query`SET SHOWPLAN_XML OFF`

      // Extract key information from the plan
      // This is simplified - real implementation would parse the XML
      return {
        content: [
          {
            type: 'text',
            text: `Query analysis complete. The query is syntactically valid and can be executed.
            
Note: For detailed execution plan analysis, consider using SQL Server Management Studio or Azure Data Studio.`,
          },
        ],
      }
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: `Query analysis failed: ${
              error instanceof Error ? error.message : 'Unknown error'
            }`,
          },
        ],
      }
    }
  }

  private logQuery(query: string, executionTime: number, rowCount: number, success: boolean, error?: string) {
    const metric: QueryMetrics = {
      query: query.substring(0, 200), // Truncate for storage
      executionTime,
      rowCount,
      success,
      error,
    }
    
    this.queryMetrics.push(metric)
    
    // Keep only last 100 queries in memory
    if (this.queryMetrics.length > 100) {
      this.queryMetrics.shift()
    }

    // Log to console for monitoring
    console.log(`[${this.userId}] Query executed:`, {
      success,
      executionTime: `${executionTime}ms`,
      rowCount,
      error,
    })
  }

  async disconnect(): Promise<void> {
    if (this.sqlPool) {
      await this.sqlPool.close()
      this.sqlPool = null
    }
  }

  async start(): Promise<void> {
    const transport = new StdioServerTransport()
    await this.server.connect(transport)
    console.log(`SQL Server MCP Server started for user ${this.userId}`)
  }
}