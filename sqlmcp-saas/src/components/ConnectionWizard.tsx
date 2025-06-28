'use client'

import React, { useState } from 'react'
import { X, ChevronRight, ChevronLeft, Database, Shield, CheckCircle, AlertCircle, Loader2 } from 'lucide-react'
import { testConnection, saveConnection } from '../services/connections'

interface ConnectionWizardProps {
  isOpen: boolean
  onClose: () => void
  onSuccess: (connection: any) => void
}

interface ConnectionData {
  name: string
  server: string
  database: string
  port: number
  username: string
  password: string
  trustServerCertificate: boolean
  encrypt: boolean
  connectionTimeout: number
}

interface ValidationErrors {
  [key: string]: string
}

export function ConnectionWizard({ isOpen, onClose, onSuccess }: ConnectionWizardProps) {
  const [currentStep, setCurrentStep] = useState(1)
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [loading, setLoading] = useState(false)
  const [testStatus, setTestStatus] = useState<'idle' | 'testing' | 'success' | 'error'>('idle')
  const [testMessage, setTestMessage] = useState('')
  const [errors, setErrors] = useState<ValidationErrors>({})
  
  const [connectionData, setConnectionData] = useState<ConnectionData>({
    name: '',
    server: '',
    database: '',
    port: 1433,
    username: '',
    password: '',
    trustServerCertificate: false,
    encrypt: true,
    connectionTimeout: 30,
  })

  if (!isOpen) return null

  const updateField = (field: keyof ConnectionData, value: any) => {
    setConnectionData(prev => ({ ...prev, [field]: value }))
    // Clear error when user starts typing
    if (errors[field]) {
      setErrors(prev => {
        const newErrors = { ...prev }
        delete newErrors[field]
        return newErrors
      })
    }
  }

  const validateStep1 = (): boolean => {
    const newErrors: ValidationErrors = {}
    
    if (!connectionData.name.trim()) {
      newErrors.name = 'Connection name is required'
    }
    if (!connectionData.server.trim()) {
      newErrors.server = 'Server is required'
    }
    if (!connectionData.database.trim()) {
      newErrors.database = 'Database is required'
    }
    if (connectionData.port < 1 || connectionData.port > 65535) {
      newErrors.port = 'Port must be between 1 and 65535'
    }

    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const validateStep2 = (): boolean => {
    const newErrors: ValidationErrors = {}
    
    if (!connectionData.username.trim()) {
      newErrors.username = 'Username is required'
    }
    if (!connectionData.password) {
      newErrors.password = 'Password is required'
    }

    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const handleNext = () => {
    if (currentStep === 1 && validateStep1()) {
      setCurrentStep(2)
    } else if (currentStep === 2 && validateStep2()) {
      setCurrentStep(3)
    }
  }

  const handleBack = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1)
      setErrors({})
    }
  }

  const handleTestConnection = async () => {
    setTestStatus('testing')
    setTestMessage('')

    try {
      const result = await testConnection({
        server: connectionData.server,
        database: connectionData.database,
        port: connectionData.port,
        username: connectionData.username,
        password: connectionData.password,
        trustServerCertificate: connectionData.trustServerCertificate,
      })

      setTestStatus('success')
      setTestMessage(result.message || 'Connection successful')
    } catch (error) {
      setTestStatus('error')
      setTestMessage(error instanceof Error ? error.message : 'Connection failed')
    }
  }

  const handleSave = async () => {
    setLoading(true)
    try {
      const saved = await saveConnection(connectionData)
      onSuccess(saved)
      onClose()
    } catch (error) {
      setTestStatus('error')
      setTestMessage(error instanceof Error ? error.message : 'Failed to save connection')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="bg-gray-50 px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-xl font-semibold text-gray-900">Add SQL Server Connection</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Progress Steps */}
        <div className="px-6 py-4 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <div className={`flex items-center ${currentStep >= 1 ? 'text-blue-600' : 'text-gray-400'}`}>
              <div className={`w-8 h-8 rounded-full flex items-center justify-center ${
                currentStep >= 1 ? 'bg-blue-600 text-white' : 'bg-gray-200'
              }`}>
                1
              </div>
              <span className="ml-2 text-sm font-medium">Connection Details</span>
            </div>
            <div className={`flex-1 h-0.5 mx-4 ${currentStep >= 2 ? 'bg-blue-600' : 'bg-gray-200'}`} />
            <div className={`flex items-center ${currentStep >= 2 ? 'text-blue-600' : 'text-gray-400'}`}>
              <div className={`w-8 h-8 rounded-full flex items-center justify-center ${
                currentStep >= 2 ? 'bg-blue-600 text-white' : 'bg-gray-200'
              }`}>
                2
              </div>
              <span className="ml-2 text-sm font-medium">Authentication</span>
            </div>
            <div className={`flex-1 h-0.5 mx-4 ${currentStep >= 3 ? 'bg-blue-600' : 'bg-gray-200'}`} />
            <div className={`flex items-center ${currentStep >= 3 ? 'text-blue-600' : 'text-gray-400'}`}>
              <div className={`w-8 h-8 rounded-full flex items-center justify-center ${
                currentStep >= 3 ? 'bg-blue-600 text-white' : 'bg-gray-200'
              }`}>
                3
              </div>
              <span className="ml-2 text-sm font-medium">Test & Save</span>
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="px-6 py-6 overflow-y-auto" style={{ maxHeight: 'calc(90vh - 240px)' }}>
          {/* Step 1: Connection Details */}
          {currentStep === 1 && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600 mb-4">
                Step 1 of 3: Connection Details
              </p>
              
              <div>
                <label htmlFor="name" className="block text-sm font-medium text-gray-700 mb-1">
                  Connection Name
                </label>
                <input
                  id="name"
                  type="text"
                  value={connectionData.name}
                  onChange={(e) => updateField('name', e.target.value)}
                  className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                    errors.name ? 'border-red-300' : 'border-gray-300'
                  }`}
                  placeholder="e.g., Production Database"
                />
                {errors.name && (
                  <p className="mt-1 text-sm text-red-600">{errors.name}</p>
                )}
              </div>

              <div>
                <label htmlFor="server" className="block text-sm font-medium text-gray-700 mb-1">
                  Server
                </label>
                <input
                  id="server"
                  type="text"
                  value={connectionData.server}
                  onChange={(e) => updateField('server', e.target.value)}
                  className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                    errors.server ? 'border-red-300' : 'border-gray-300'
                  }`}
                  placeholder="e.g., localhost or sql.example.com"
                />
                {errors.server && (
                  <p className="mt-1 text-sm text-red-600">{errors.server}</p>
                )}
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div className="col-span-2">
                  <label htmlFor="database" className="block text-sm font-medium text-gray-700 mb-1">
                    Database
                  </label>
                  <input
                    id="database"
                    type="text"
                    value={connectionData.database}
                    onChange={(e) => updateField('database', e.target.value)}
                    className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                      errors.database ? 'border-red-300' : 'border-gray-300'
                    }`}
                    placeholder="e.g., myapp_production"
                  />
                  {errors.database && (
                    <p className="mt-1 text-sm text-red-600">{errors.database}</p>
                  )}
                </div>
                <div>
                  <label htmlFor="port" className="block text-sm font-medium text-gray-700 mb-1">
                    Port
                  </label>
                  <input
                    id="port"
                    type="number"
                    value={connectionData.port}
                    onChange={(e) => updateField('port', parseInt(e.target.value) || 1433)}
                    className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                      errors.port ? 'border-red-300' : 'border-gray-300'
                    }`}
                  />
                  {errors.port && (
                    <p className="mt-1 text-sm text-red-600">{errors.port}</p>
                  )}
                </div>
              </div>

              {/* Advanced Options */}
              <div className="pt-4">
                <button
                  type="button"
                  onClick={() => setShowAdvanced(!showAdvanced)}
                  className="text-sm text-blue-600 hover:text-blue-700 font-medium"
                >
                  {showAdvanced ? '− Hide' : '+ Show'} Advanced Options
                </button>
                
                {showAdvanced && (
                  <div className="mt-4 space-y-4 p-4 bg-gray-50 rounded-lg">
                    <div className="flex items-center">
                      <input
                        id="trustServerCertificate"
                        type="checkbox"
                        checked={connectionData.trustServerCertificate}
                        onChange={(e) => updateField('trustServerCertificate', e.target.checked)}
                        className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                      />
                      <label htmlFor="trustServerCertificate" className="ml-2 text-sm text-gray-700">
                        Trust Server Certificate
                      </label>
                    </div>

                    <div className="flex items-center">
                      <input
                        id="encrypt"
                        type="checkbox"
                        checked={connectionData.encrypt}
                        onChange={(e) => updateField('encrypt', e.target.checked)}
                        className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                      />
                      <label htmlFor="encrypt" className="ml-2 text-sm text-gray-700">
                        Encrypt Connection
                      </label>
                    </div>

                    <div>
                      <label htmlFor="connectionTimeout" className="block text-sm font-medium text-gray-700 mb-1">
                        Connection Timeout (seconds)
                      </label>
                      <input
                        id="connectionTimeout"
                        type="number"
                        value={connectionData.connectionTimeout}
                        onChange={(e) => updateField('connectionTimeout', parseInt(e.target.value) || 30)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Step 2: Authentication */}
          {currentStep === 2 && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600 mb-4">
                Step 2 of 3: Authentication
              </p>

              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
                <div className="flex">
                  <Shield className="h-5 w-5 text-blue-600 mt-0.5" />
                  <div className="ml-3">
                    <h4 className="text-sm font-medium text-blue-900">SQL Server Authentication</h4>
                    <p className="text-sm text-blue-700 mt-1">
                      Enter the credentials for a SQL Server user with appropriate permissions.
                    </p>
                  </div>
                </div>
              </div>

              <div>
                <label htmlFor="username" className="block text-sm font-medium text-gray-700 mb-1">
                  Username
                </label>
                <input
                  id="username"
                  type="text"
                  value={connectionData.username}
                  onChange={(e) => updateField('username', e.target.value)}
                  className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                    errors.username ? 'border-red-300' : 'border-gray-300'
                  }`}
                  placeholder="e.g., sa or app_user"
                />
                {errors.username && (
                  <p className="mt-1 text-sm text-red-600">{errors.username}</p>
                )}
              </div>

              <div>
                <label htmlFor="password" className="block text-sm font-medium text-gray-700 mb-1">
                  Password
                </label>
                <input
                  id="password"
                  type="password"
                  value={connectionData.password}
                  onChange={(e) => updateField('password', e.target.value)}
                  className={`w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                    errors.password ? 'border-red-300' : 'border-gray-300'
                  }`}
                  placeholder="••••••••"
                />
                {errors.password && (
                  <p className="mt-1 text-sm text-red-600">{errors.password}</p>
                )}
              </div>

              <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
                <h4 className="text-sm font-medium text-gray-900 mb-2">Security Note</h4>
                <p className="text-sm text-gray-600">
                  Your password will be encrypted before storage. We recommend using a dedicated 
                  read-only account for this connection.
                </p>
              </div>
            </div>
          )}

          {/* Step 3: Test & Save */}
          {currentStep === 3 && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600 mb-4">
                Step 3 of 3: Test & Save
              </p>

              <div className="bg-gray-50 rounded-lg p-6">
                <h4 className="text-lg font-medium text-gray-900 mb-4">Connection Summary</h4>
                
                <dl className="space-y-2">
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-600">Name:</dt>
                    <dd className="text-sm font-medium text-gray-900">{connectionData.name}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-600">Server:</dt>
                    <dd className="text-sm font-medium text-gray-900">{connectionData.server}:{connectionData.port}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-600">Database:</dt>
                    <dd className="text-sm font-medium text-gray-900">{connectionData.database}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-600">Username:</dt>
                    <dd className="text-sm font-medium text-gray-900">{connectionData.username}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-600">Encryption:</dt>
                    <dd className="text-sm font-medium text-gray-900">{connectionData.encrypt ? 'Enabled' : 'Disabled'}</dd>
                  </div>
                </dl>
              </div>

              {/* Test Connection Area */}
              <div className="border border-gray-200 rounded-lg p-6">
                <div className="flex items-center justify-between mb-4">
                  <h4 className="text-md font-medium text-gray-900">Test Connection</h4>
                  <button
                    onClick={handleTestConnection}
                    disabled={testStatus === 'testing'}
                    className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition flex items-center"
                  >
                    {testStatus === 'testing' ? (
                      <>
                        <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        Testing...
                      </>
                    ) : (
                      <>
                        <Database className="h-4 w-4 mr-2" />
                        Test Connection
                      </>
                    )}
                  </button>
                </div>

                {testStatus !== 'idle' && (
                  <div className={`rounded-lg p-4 ${
                    testStatus === 'success' ? 'bg-green-50 border border-green-200' :
                    testStatus === 'error' ? 'bg-red-50 border border-red-200' :
                    'bg-blue-50 border border-blue-200'
                  }`}>
                    <div className="flex items-start">
                      {testStatus === 'success' && <CheckCircle className="h-5 w-5 text-green-600 mt-0.5" />}
                      {testStatus === 'error' && <AlertCircle className="h-5 w-5 text-red-600 mt-0.5" />}
                      {testStatus === 'testing' && <Loader2 className="h-5 w-5 text-blue-600 mt-0.5 animate-spin" />}
                      <p className={`ml-3 text-sm ${
                        testStatus === 'success' ? 'text-green-800' :
                        testStatus === 'error' ? 'text-red-800' :
                        'text-blue-800'
                      }`}>
                        {testMessage || (testStatus === 'testing' ? 'Testing connection...' : '')}
                      </p>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="bg-gray-50 px-6 py-4 border-t border-gray-200 flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 hover:text-gray-900 transition"
          >
            Cancel
          </button>
          
          <div className="flex items-center space-x-3">
            {currentStep > 1 && (
              <button
                onClick={handleBack}
                className="px-4 py-2 text-gray-700 hover:text-gray-900 transition flex items-center"
              >
                <ChevronLeft className="h-4 w-4 mr-1" />
                Back
              </button>
            )}
            
            {currentStep < 3 && (
              <button
                onClick={handleNext}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition flex items-center"
              >
                Next
                <ChevronRight className="h-4 w-4 ml-1" />
              </button>
            )}
            
            {currentStep === 3 && (
              <button
                onClick={handleSave}
                disabled={testStatus !== 'success' || loading}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition flex items-center"
              >
                {loading ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <CheckCircle className="h-4 w-4 mr-2" />
                    Save Connection
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}