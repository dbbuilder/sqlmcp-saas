import { NextApiRequest, NextApiResponse } from 'next'
import { getServerSession } from 'next-auth'
import sql from 'mssql'
import { authOptions } from '../auth/[...nextauth]'

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse
) {
  // Only allow POST requests
  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method not allowed' })
  }

  // Check authentication
  const session = await getServerSession(req, res, authOptions)
  if (!session) {
    return res.status(401).json({ error: 'Unauthorized' })
  }

  const { server, database, port, username, password, trustServerCertificate } = req.body

  // Validate required fields
  if (!server || !database || !username || !password) {
    return res.status(400).json({ error: 'Missing required connection parameters' })
  }

  // Configure SQL connection
  const config: sql.config = {
    server,
    database,
    user: username,
    password,
    port: port || 1433,
    options: {
      encrypt: true, // Always encrypt for security
      trustServerCertificate: trustServerCertificate || false,
      enableArithAbort: true,
      connectionTimeout: 15000, // 15 second timeout for testing
      requestTimeout: 15000,
    },
    pool: {
      max: 1, // Only need one connection for testing
      min: 0,
      idleTimeoutMillis: 5000,
    },
  }

  let connection: sql.ConnectionPool | null = null

  try {
    // Attempt to connect
    connection = new sql.ConnectionPool(config)
    await connection.connect()

    // Run a simple test query
    const result = await connection.request().query`SELECT @@VERSION as version, DB_NAME() as database_name`
    
    if (result.recordset && result.recordset.length > 0) {
      const versionInfo = result.recordset[0].version
      const dbName = result.recordset[0].database_name
      
      return res.status(200).json({
        success: true,
        message: `Connection successful! Connected to database '${dbName}'`,
        details: {
          database: dbName,
          serverVersion: versionInfo.split('\n')[0], // First line of version info
        }
      })
    }

    return res.status(200).json({
      success: true,
      message: 'Connection successful',
    })

  } catch (error) {
    console.error('Connection test failed:', error)
    
    // Provide user-friendly error messages
    let errorMessage = 'Connection failed'
    
    if (error instanceof Error) {
      if (error.message.includes('Login failed')) {
        errorMessage = 'Invalid credentials. Please check your username and password.'
      } else if (error.message.includes('Cannot open database')) {
        errorMessage = `Cannot open database "${database}". Verify the database name is correct.`
      } else if (error.message.includes('getaddrinfo ENOTFOUND') || error.message.includes('ECONNREFUSED')) {
        errorMessage = `Cannot connect to server "${server}". Please check the server address and port.`
      } else if (error.message.includes('self signed certificate')) {
        errorMessage = 'Server certificate validation failed. Enable "Trust Server Certificate" if this is expected.'
      } else if (error.message.includes('timeout')) {
        errorMessage = 'Connection timeout. The server may be unreachable or slow to respond.'
      } else {
        errorMessage = error.message
      }
    }

    return res.status(400).json({
      success: false,
      error: errorMessage,
    })

  } finally {
    // Always close the connection
    if (connection) {
      try {
        await connection.close()
      } catch (closeError) {
        console.error('Error closing connection:', closeError)
      }
    }
  }
}