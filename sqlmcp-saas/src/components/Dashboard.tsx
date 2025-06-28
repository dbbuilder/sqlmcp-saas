'use client'

import React from 'react'
import { Database, Key, Activity, Plus, Settings, ExternalLink } from 'lucide-react'

interface Connection {
  id: string
  name: string
  server: string
  database: string
  isActive: boolean
  lastTested: Date | null
}

interface DashboardProps {
  user: {
    name: string
    email: string
    plan: 'FREE' | 'STARTER' | 'PRO' | 'ENTERPRISE'
  }
  connections: Connection[]
  usage: {
    queriesThisMonth: number
    queryLimit: number
    connectionsUsed: number
    connectionLimit: number
  }
}

export default function Dashboard({ user, connections, usage }: DashboardProps) {
  const [selectedConnection, setSelectedConnection] = React.useState<string | null>(null)

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm border-b">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-16">
            <div className="flex items-center">
              <h1 className="text-xl font-semibold">SQLMCP Dashboard</h1>
            </div>
            <div className="flex items-center space-x-4">
              <span className="text-sm text-gray-500">
                {user.plan} Plan
              </span>
              <button className="p-2 rounded-lg hover:bg-gray-100">
                <Settings className="h-5 w-5 text-gray-600" />
              </button>
            </div>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Usage Stats */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Queries This Month</p>
                <p className="text-2xl font-semibold mt-1">
                  {usage.queriesThisMonth.toLocaleString()}
                </p>
                <p className="text-xs text-gray-500 mt-1">
                  of {usage.queryLimit.toLocaleString()} limit
                </p>
              </div>
              <Activity className="h-8 w-8 text-blue-500" />
            </div>
            <div className="mt-4">
              <div className="bg-gray-200 rounded-full h-2">
                <div
                  className="bg-blue-500 h-2 rounded-full"
                  style={{
                    width: `${(usage.queriesThisMonth / usage.queryLimit) * 100}%`
                  }}
                />
              </div>
            </div>
          </div>

          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Active Connections</p>
                <p className="text-2xl font-semibold mt-1">
                  {usage.connectionsUsed}
                </p>
                <p className="text-xs text-gray-500 mt-1">
                  of {usage.connectionLimit} allowed
                </p>
              </div>
              <Database className="h-8 w-8 text-green-500" />
            </div>
          </div>

          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">API Keys</p>
                <p className="text-2xl font-semibold mt-1">2</p>
                <p className="text-xs text-gray-500 mt-1">Active keys</p>
              </div>
              <Key className="h-8 w-8 text-purple-500" />
            </div>
          </div>
        </div>

        {/* Connections */}
        <div className="bg-white rounded-lg shadow">
          <div className="px-6 py-4 border-b border-gray-200">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">SQL Server Connections</h2>
              <button className="flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
                <Plus className="h-4 w-4 mr-2" />
                Add Connection
              </button>
            </div>
          </div>

          <div className="divide-y divide-gray-200">
            {connections.length === 0 ? (
              <div className="px-6 py-12 text-center">
                <Database className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                <p className="text-gray-500 mb-4">No connections yet</p>
                <button className="text-blue-600 hover:text-blue-700 font-medium">
                  Add your first connection →
                </button>
              </div>
            ) : (
              connections.map((connection) => (
                <div
                  key={connection.id}
                  className="px-6 py-4 hover:bg-gray-50 cursor-pointer"
                  onClick={() => setSelectedConnection(connection.id)}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-3">
                      <div
                        className={`h-3 w-3 rounded-full ${
                          connection.isActive ? 'bg-green-500' : 'bg-gray-300'
                        }`}
                      />
                      <div>
                        <p className="font-medium">{connection.name}</p>
                        <p className="text-sm text-gray-500">
                          {connection.server} / {connection.database}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center space-x-4">
                      <p className="text-sm text-gray-500">
                        Last tested:{' '}
                        {connection.lastTested
                          ? new Date(connection.lastTested).toLocaleDateString()
                          : 'Never'}
                      </p>
                      <ExternalLink className="h-4 w-4 text-gray-400" />
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Quick Start */}
        <div className="mt-8 bg-blue-50 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-2">
            Quick Start Guide
          </h3>
          <p className="text-blue-700 mb-4">
            Connect your SQL Server to AI models in 3 simple steps:
          </p>
          <ol className="space-y-2 text-sm text-blue-700">
            <li>1. Add a SQL Server connection using the button above</li>
            <li>2. Copy your MCP endpoint URL from the API Keys section</li>
            <li>3. Configure Claude or ChatGPT to use your MCP endpoint</li>
          </ol>
          <button className="mt-4 text-blue-600 hover:text-blue-700 font-medium text-sm">
            View full documentation →
          </button>
        </div>
      </div>
    </div>
  )
}