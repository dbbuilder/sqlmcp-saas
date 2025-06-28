import { vi, describe, it, expect, beforeEach } from 'vitest'
import { testConnection, saveConnection, getConnections, deleteConnection } from './connections'

// Mock fetch globally
global.fetch = vi.fn()

describe('Connections Service', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('testConnection', () => {
    it('should successfully test a valid connection', async () => {
      const mockResponse = { success: true, message: 'Connection successful' }
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      })

      const connectionData = {
        server: 'localhost',
        database: 'testdb',
        username: 'sa',
        password: 'password',
        port: 1433,
      }

      const result = await testConnection(connectionData)

      expect(global.fetch).toHaveBeenCalledWith('/api/connections/test', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(connectionData),
      })
      expect(result).toEqual(mockResponse)
    })

    it('should handle connection test failure', async () => {
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Invalid credentials' }),
      })

      const connectionData = {
        server: 'localhost',
        database: 'testdb',
        username: 'invalid',
        password: 'wrong',
        port: 1433,
      }

      await expect(testConnection(connectionData)).rejects.toThrow('Invalid credentials')
    })

    it('should handle network errors', async () => {
      ;(global.fetch as any).mockRejectedValueOnce(new Error('Network error'))

      const connectionData = {
        server: 'unreachable',
        database: 'testdb',
        username: 'sa',
        password: 'password',
        port: 1433,
      }

      await expect(testConnection(connectionData)).rejects.toThrow('Network error')
    })
  })

  describe('saveConnection', () => {
    it('should save a new connection', async () => {
      const mockResponse = { id: '123', name: 'Production DB' }
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      })

      const connectionData = {
        name: 'Production DB',
        server: 'sql.example.com',
        database: 'prod_db',
        username: 'app_user',
        password: 'secure_password',
        port: 1433,
        trustServerCertificate: false,
      }

      const result = await saveConnection(connectionData)

      expect(global.fetch).toHaveBeenCalledWith('/api/connections', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(connectionData),
      })
      expect(result).toEqual(mockResponse)
    })

    it('should handle save errors', async () => {
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 409,
        json: async () => ({ error: 'Connection name already exists' }),
      })

      const connectionData = {
        name: 'Duplicate Name',
        server: 'localhost',
        database: 'testdb',
        username: 'sa',
        password: 'password',
        port: 1433,
      }

      await expect(saveConnection(connectionData)).rejects.toThrow('Connection name already exists')
    })
  })

  describe('getConnections', () => {
    it('should fetch all connections', async () => {
      const mockConnections = [
        { id: '1', name: 'Dev DB', server: 'localhost', database: 'dev' },
        { id: '2', name: 'Prod DB', server: 'sql.example.com', database: 'prod' },
      ]
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => mockConnections,
      })

      const result = await getConnections()

      expect(global.fetch).toHaveBeenCalledWith('/api/connections', {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      })
      expect(result).toEqual(mockConnections)
    })

    it('should handle empty connections list', async () => {
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => [],
      })

      const result = await getConnections()
      expect(result).toEqual([])
    })
  })

  describe('deleteConnection', () => {
    it('should delete a connection', async () => {
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })

      const connectionId = '123'
      await deleteConnection(connectionId)

      expect(global.fetch).toHaveBeenCalledWith(`/api/connections/${connectionId}`, {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
        },
      })
    })

    it('should handle delete errors', async () => {
      ;(global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Connection not found' }),
      })

      await expect(deleteConnection('invalid-id')).rejects.toThrow('Connection not found')
    })
  })
})