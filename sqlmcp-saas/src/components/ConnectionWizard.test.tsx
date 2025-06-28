import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConnectionWizard } from './ConnectionWizard'
import { vi } from 'vitest'

// Mock the API calls
const mockTestConnection = vi.fn()
const mockSaveConnection = vi.fn()
const mockOnClose = vi.fn()
const mockOnSuccess = vi.fn()

vi.mock('../services/connections', () => ({
  testConnection: mockTestConnection,
  saveConnection: mockSaveConnection,
}))

describe('ConnectionWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('should render the connection wizard with initial step', () => {
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    expect(screen.getByText('Add SQL Server Connection')).toBeInTheDocument()
    expect(screen.getByText('Step 1 of 3: Connection Details')).toBeInTheDocument()
    expect(screen.getByLabelText('Connection Name')).toBeInTheDocument()
    expect(screen.getByLabelText('Server')).toBeInTheDocument()
    expect(screen.getByLabelText('Database')).toBeInTheDocument()
  })

  it('should validate required fields before proceeding to next step', async () => {
    const user = userEvent.setup()
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    const nextButton = screen.getByText('Next')
    await user.click(nextButton)

    expect(screen.getByText('Connection name is required')).toBeInTheDocument()
    expect(screen.getByText('Server is required')).toBeInTheDocument()
    expect(screen.getByText('Database is required')).toBeInTheDocument()
  })

  it('should proceed to authentication step when connection details are valid', async () => {
    const user = userEvent.setup()
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Fill in connection details
    await user.type(screen.getByLabelText('Connection Name'), 'Production DB')
    await user.type(screen.getByLabelText('Server'), 'sql.example.com')
    await user.type(screen.getByLabelText('Database'), 'myapp_prod')
    await user.type(screen.getByLabelText('Port'), '1433')

    await user.click(screen.getByText('Next'))

    expect(screen.getByText('Step 2 of 3: Authentication')).toBeInTheDocument()
    expect(screen.getByLabelText('Username')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('should test connection before final save', async () => {
    const user = userEvent.setup()
    mockTestConnection.mockResolvedValueOnce({ success: true, message: 'Connection successful' })

    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Fill step 1
    await user.type(screen.getByLabelText('Connection Name'), 'Test DB')
    await user.type(screen.getByLabelText('Server'), 'localhost')
    await user.type(screen.getByLabelText('Database'), 'testdb')
    await user.click(screen.getByText('Next'))

    // Fill step 2
    await user.type(screen.getByLabelText('Username'), 'sa')
    await user.type(screen.getByLabelText('Password'), 'MyStr0ngP@ssw0rd')
    await user.click(screen.getByText('Next'))

    // Step 3 - Test connection
    expect(screen.getByText('Step 3 of 3: Test & Save')).toBeInTheDocument()
    
    const testButton = screen.getByText('Test Connection')
    await user.click(testButton)

    await waitFor(() => {
      expect(mockTestConnection).toHaveBeenCalledWith({
        name: 'Test DB',
        server: 'localhost',
        database: 'testdb',
        port: 1433,
        username: 'sa',
        password: 'MyStr0ngP@ssw0rd',
        trustServerCertificate: false,
      })
    })

    expect(screen.getByText('Connection successful')).toBeInTheDocument()
    expect(screen.getByText('Save Connection')).not.toBeDisabled()
  })

  it('should display error when connection test fails', async () => {
    const user = userEvent.setup()
    mockTestConnection.mockRejectedValueOnce(new Error('Cannot connect to server'))

    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Navigate to test step
    await user.type(screen.getByLabelText('Connection Name'), 'Test DB')
    await user.type(screen.getByLabelText('Server'), 'invalid.server')
    await user.type(screen.getByLabelText('Database'), 'testdb')
    await user.click(screen.getByText('Next'))

    await user.type(screen.getByLabelText('Username'), 'sa')
    await user.type(screen.getByLabelText('Password'), 'password')
    await user.click(screen.getByText('Next'))

    await user.click(screen.getByText('Test Connection'))

    await waitFor(() => {
      expect(screen.getByText('Cannot connect to server')).toBeInTheDocument()
    })

    expect(screen.getByText('Save Connection')).toBeDisabled()
  })

  it('should save connection and call onSuccess', async () => {
    const user = userEvent.setup()
    mockTestConnection.mockResolvedValueOnce({ success: true })
    mockSaveConnection.mockResolvedValueOnce({ id: '123', name: 'Test DB' })

    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Fill all steps
    await user.type(screen.getByLabelText('Connection Name'), 'Test DB')
    await user.type(screen.getByLabelText('Server'), 'localhost')
    await user.type(screen.getByLabelText('Database'), 'testdb')
    await user.click(screen.getByText('Next'))

    await user.type(screen.getByLabelText('Username'), 'sa')
    await user.type(screen.getByLabelText('Password'), 'password')
    await user.click(screen.getByText('Next'))

    await user.click(screen.getByText('Test Connection'))
    await waitFor(() => expect(screen.getByText('Connection successful')).toBeInTheDocument())

    await user.click(screen.getByText('Save Connection'))

    await waitFor(() => {
      expect(mockSaveConnection).toHaveBeenCalledWith({
        name: 'Test DB',
        server: 'localhost',
        database: 'testdb',
        port: 1433,
        username: 'sa',
        password: 'password',
        trustServerCertificate: false,
      })
      expect(mockOnSuccess).toHaveBeenCalledWith({ id: '123', name: 'Test DB' })
    })
  })

  it('should allow going back to previous steps', async () => {
    const user = userEvent.setup()
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Go to step 2
    await user.type(screen.getByLabelText('Connection Name'), 'Test DB')
    await user.type(screen.getByLabelText('Server'), 'localhost')
    await user.type(screen.getByLabelText('Database'), 'testdb')
    await user.click(screen.getByText('Next'))

    expect(screen.getByText('Step 2 of 3: Authentication')).toBeInTheDocument()

    // Go back to step 1
    await user.click(screen.getByText('Back'))

    expect(screen.getByText('Step 1 of 3: Connection Details')).toBeInTheDocument()
    // Values should be preserved
    expect(screen.getByDisplayValue('Test DB')).toBeInTheDocument()
    expect(screen.getByDisplayValue('localhost')).toBeInTheDocument()
    expect(screen.getByDisplayValue('testdb')).toBeInTheDocument()
  })

  it('should close wizard when cancel is clicked', async () => {
    const user = userEvent.setup()
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    await user.click(screen.getByText('Cancel'))
    expect(mockOnClose).toHaveBeenCalled()
  })

  it('should handle advanced options', async () => {
    const user = userEvent.setup()
    render(
      <ConnectionWizard
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
      />
    )

    // Toggle advanced options
    await user.click(screen.getByText('Advanced Options'))

    expect(screen.getByLabelText('Trust Server Certificate')).toBeInTheDocument()
    expect(screen.getByLabelText('Connection Timeout (seconds)')).toBeInTheDocument()
    expect(screen.getByLabelText('Encrypt Connection')).toBeInTheDocument()
  })
})