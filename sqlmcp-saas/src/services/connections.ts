// Connection service for API calls related to SQL Server connections

interface ConnectionTestData {
  server: string
  database: string
  port: number
  username: string
  password: string
  trustServerCertificate?: boolean
}

interface ConnectionData extends ConnectionTestData {
  name: string
  encrypt?: boolean
  connectionTimeout?: number
}

interface TestConnectionResponse {
  success: boolean
  message: string
}

interface SavedConnection {
  id: string
  name: string
  server: string
  database: string
  createdAt?: string
}

// Helper function to handle API responses
async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json()
    throw new Error(error.error || `HTTP error! status: ${response.status}`)
  }
  return response.json()
}

// Test a SQL Server connection
export async function testConnection(data: ConnectionTestData): Promise<TestConnectionResponse> {
  const response = await fetch('/api/connections/test', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(data),
  })

  return handleResponse<TestConnectionResponse>(response)
}

// Save a new connection
export async function saveConnection(data: ConnectionData): Promise<SavedConnection> {
  const response = await fetch('/api/connections', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(data),
  })

  return handleResponse<SavedConnection>(response)
}

// Get all connections for the current user
export async function getConnections(): Promise<SavedConnection[]> {
  const response = await fetch('/api/connections', {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
    },
  })

  return handleResponse<SavedConnection[]>(response)
}

// Delete a connection
export async function deleteConnection(connectionId: string): Promise<void> {
  const response = await fetch(`/api/connections/${connectionId}`, {
    method: 'DELETE',
    headers: {
      'Content-Type': 'application/json',
    },
  })

  await handleResponse<{ success: boolean }>(response)
}

// Update a connection
export async function updateConnection(connectionId: string, data: Partial<ConnectionData>): Promise<SavedConnection> {
  const response = await fetch(`/api/connections/${connectionId}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(data),
  })

  return handleResponse<SavedConnection>(response)
}