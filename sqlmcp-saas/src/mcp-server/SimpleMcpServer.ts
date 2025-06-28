import { Server } from '@modelcontextprotocol/sdk/server/index.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js'
import sql from 'mssql'

interface ConnectionConfig {
  server: string
  database: string
  user: string
  password: string
  options: {
    encrypt: boolean
    trustServerCertificate: boolean
    enableArithAbort: boolean
  }
}

export class SimpleMcpServer {
  private server: Server
  private sqlPool: sql.ConnectionPool | null = null
  private config: ConnectionConfig | null = null
  private queryLimit: number
  private userId: string

  constructor(userId: string, queryLimit: number = 1000) {
    this.userId = userId
    this.queryLimit = queryLimit
    this.server = new Server(
      {
        name: 'sqlmcp-simple',
        version: '0.1.0',
      },
      {
        capabilities: {
          tools: {},
        },
      }
    )

    this.setupHandlers()
  }

  private setupHandlers(): void {
    this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
      tools: [
        {
          name: 'list_tables',
          description: 'List all available tables in the database',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'list_views',
          description: 'List all available views in the database',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
        {
          name: 'describe_table',
          description: 'Get schema information for a specific table',
          inputSchema: {
            type: 'object',
            properties: {
              tableName: {
                type: 'string',
                description: 'Name of the table to describe',
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
          name: 'test_connection',
          description: 'Test the database connection',
          inputSchema: {
            type: 'object',
            properties: {},
          },
        },
      ],
    }))

    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params

      if (!this.sqlPool || !this.sqlPool.connected) {
        throw new Error('Database not connected. Please connect first.')
      }

      try {
        switch (name) {
          case 'list_tables':
            return await this.listTables()
          case 'list_views':
            return await this.listViews()
          case 'describe_table':
            return await this.describeTable(args.tableName as string)
          case 'query_data':
            return await this.queryData(
              args.query as string,
              args.limit as number || 100
            )
          case 'test_connection':
            return await this.testConnection()
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

  async connect(config: ConnectionConfig): Promise<void> {
    this.config = config
    this.sqlPool = await sql.connect(config)
    console.log(`Connected to SQL Server: ${config.server}/${config.database}`)
  }

  async disconnect(): Promise<void> {
    if (this.sqlPool) {
      await this.sqlPool.close()
      this.sqlPool = null
    }
  }

  private async listTables() {
    const result = await this.sqlPool!.request().query`
      SELECT 
        TABLE_SCHEMA,
        TABLE_NAME,
        (
          SELECT COUNT(*) 
          FROM INFORMATION_SCHEMA.COLUMNS c 
          WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA 
          AND c.TABLE_NAME = t.TABLE_NAME
        ) as COLUMN_COUNT
      FROM INFORMATION_SCHEMA.TABLES t
      WHERE TABLE_TYPE = 'BASE TABLE'
      AND TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
      ORDER BY TABLE_SCHEMA, TABLE_NAME
    `

    return {
      content: [
        {
          type: 'text',
          text: `Found ${result.recordset.length} tables:\n\n${result.recordset
            .map(
              (row) =>
                `- ${row.TABLE_SCHEMA}.${row.TABLE_NAME} (${row.COLUMN_COUNT} columns)`
            )
            .join('\n')}`,
        },
      ],
    }
  }

  private async listViews() {
    const result = await this.sqlPool!.request().query`
      SELECT 
        TABLE_SCHEMA,
        TABLE_NAME as VIEW_NAME
      FROM INFORMATION_SCHEMA.VIEWS
      WHERE TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
      ORDER BY TABLE_SCHEMA, TABLE_NAME
    `

    return {
      content: [
        {
          type: 'text',
          text: `Found ${result.recordset.length} views:\n\n${result.recordset
            .map((row) => `- ${row.TABLE_SCHEMA}.${row.VIEW_NAME}`)
            .join('\n')}`,
        },
      ],
    }
  }

  private async describeTable(tableName: string) {
    // Validate table name to prevent SQL injection
    if (!/^[a-zA-Z0-9_.\[\]]+$/.test(tableName)) {
      throw new Error('Invalid table name')
    }

    const result = await this.sqlPool!.request()
      .input('tableName', sql.NVarChar, tableName).query`
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
        END AS IS_PRIMARY_KEY
      FROM INFORMATION_SCHEMA.COLUMNS c
      LEFT JOIN (
        SELECT 
          ku.TABLE_SCHEMA,
          ku.TABLE_NAME,
          ku.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
          ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
          AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
      ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
        AND c.TABLE_NAME = pk.TABLE_NAME 
        AND c.COLUMN_NAME = pk.COLUMN_NAME
      WHERE c.TABLE_NAME = @tableName
      ORDER BY c.ORDINAL_POSITION
    `

    if (result.recordset.length === 0) {
      throw new Error(`Table '${tableName}' not found`)
    }

    const columns = result.recordset.map((col) => {
      let type = col.DATA_TYPE
      if (col.CHARACTER_MAXIMUM_LENGTH) {
        type += `(${col.CHARACTER_MAXIMUM_LENGTH})`
      } else if (col.NUMERIC_PRECISION) {
        type += `(${col.NUMERIC_PRECISION}${
          col.NUMERIC_SCALE ? `,${col.NUMERIC_SCALE}` : ''
        })`
      }

      return `- ${col.COLUMN_NAME}: ${type} ${
        col.IS_NULLABLE === 'NO' ? 'NOT NULL' : 'NULL'
      }${col.IS_PRIMARY_KEY === 'YES' ? ' PRIMARY KEY' : ''}${
        col.COLUMN_DEFAULT ? ` DEFAULT ${col.COLUMN_DEFAULT}` : ''
      }`
    })

    return {
      content: [
        {
          type: 'text',
          text: `Table: ${tableName}\n\nColumns:\n${columns.join('\n')}`,
        },
      ],
    }
  }

  private async queryData(query: string, limit: number) {
    // Basic query validation
    const normalizedQuery = query.trim().toUpperCase()
    
    // Check if it's a SELECT query
    if (!normalizedQuery.startsWith('SELECT')) {
      throw new Error('Only SELECT queries are allowed')
    }

    // Check for dangerous keywords
    const dangerousKeywords = [
      'INSERT', 'UPDATE', 'DELETE', 'DROP', 'CREATE', 'ALTER', 'EXEC',
      'EXECUTE', 'SP_', 'XP_', 'GRANT', 'REVOKE', 'DENY'
    ]
    
    for (const keyword of dangerousKeywords) {
      if (normalizedQuery.includes(keyword)) {
        throw new Error(`Query contains forbidden keyword: ${keyword}`)
      }
    }

    // Apply row limit
    const effectiveLimit = Math.min(limit, this.queryLimit)
    let limitedQuery = query

    if (!normalizedQuery.includes('TOP')) {
      limitedQuery = query.replace(
        /SELECT/i,
        `SELECT TOP ${effectiveLimit}`
      )
    }

    try {
      const result = await this.sqlPool!.request().query(limitedQuery)

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

      // Format results as a table
      const columns = Object.keys(result.recordset[0])
      const rows = result.recordset.map((row) =>
        columns.map((col) => {
          const value = row[col]
          if (value === null) return 'NULL'
          if (value instanceof Date) return value.toISOString()
          return String(value)
        })
      )

      // Create ASCII table
      const columnWidths = columns.map((col, i) =>
        Math.max(
          col.length,
          ...rows.map((row) => row[i].length)
        )
      )

      const separator = '+' + columnWidths.map((w) => '-'.repeat(w + 2)).join('+') + '+'
      const header = '|' + columns.map((col, i) => ` ${col.padEnd(columnWidths[i])} `).join('|') + '|'
      const rowsFormatted = rows.map(
        (row) => '|' + row.map((cell, i) => ` ${cell.padEnd(columnWidths[i])} `).join('|') + '|'
      )

      const table = [
        separator,
        header,
        separator,
        ...rowsFormatted,
        separator,
        `\n${result.recordset.length} rows returned${
          result.recordset.length === effectiveLimit ? ' (limit reached)' : ''
        }`,
      ].join('\n')

      // Log query for audit
      console.log(`User ${this.userId} executed query: ${limitedQuery}`)

      return {
        content: [
          {
            type: 'text',
            text: table,
          },
        ],
      }
    } catch (error) {
      throw new Error(
        `Query execution failed: ${
          error instanceof Error ? error.message : 'Unknown error'
        }`
      )
    }
  }

  private async testConnection() {
    try {
      const result = await this.sqlPool!.request().query`SELECT @@VERSION as version`
      return {
        content: [
          {
            type: 'text',
            text: `Connection successful!\n\nServer: ${this.config?.server}\nDatabase: ${this.config?.database}\nVersion: ${result.recordset[0].version}`,
          },
        ],
      }
    } catch (error) {
      throw new Error(
        `Connection test failed: ${
          error instanceof Error ? error.message : 'Unknown error'
        }`
      )
    }
  }

  async start(): Promise<void> {
    const transport = new StdioServerTransport()
    await this.server.connect(transport)
    console.log('MCP Server started')
  }
}