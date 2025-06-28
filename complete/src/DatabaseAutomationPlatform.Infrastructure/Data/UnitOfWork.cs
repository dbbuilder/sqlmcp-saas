using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using DatabaseAutomationPlatform.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for managing transactions across repositories
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly IStoredProcedureExecutor _executor;
    private readonly ILogger<UnitOfWork> _logger;
    
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    private IAuditEventRepository? _auditEvents;
    private IDatabaseTaskRepository? _databaseTasks;
    private IDataClassificationRepository? _dataClassifications;

    public UnitOfWork(
        IDatabaseConnectionFactory connectionFactory,
        IStoredProcedureExecutor executor,
        ILogger<UnitOfWork> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAuditEventRepository AuditEvents
    {
        get
        {
            if (_auditEvents == null)
            {
                var logger = LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<AuditEventRepository>();
                _auditEvents = new AuditEventRepository(_executor, logger);
            }
            return _auditEvents;
        }
    }

    public IDatabaseTaskRepository DatabaseTasks
    {
        get
        {
            if (_databaseTasks == null)
            {
                var logger = LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<DatabaseTaskRepository>();
                _databaseTasks = new DatabaseTaskRepository(_executor, logger);
            }
            return _databaseTasks;
        }
    }

    public IDataClassificationRepository DataClassifications
    {
        get
        {
            if (_dataClassifications == null)
            {
                var logger = LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<DataClassificationRepository>();
                _dataClassifications = new DataClassificationRepository(_executor, logger);
            }
            return _dataClassifications;
        }
    }

    public bool HasActiveTransaction => _transaction != null;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already active");
        }

        _logger.LogInformation("Beginning transaction");

        if (_connection == null)
        {
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        }

        _transaction = _connection.BeginTransaction();
        
        // Configure executor to use this transaction
        if (_executor is TransactionalStoredProcedureExecutor transactionalExecutor)
        {
            transactionalExecutor.SetTransaction(_transaction);
        }

        _logger.LogInformation("Transaction started");
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit");
        }

        try
        {
            _logger.LogInformation("Committing transaction");
            _transaction.Commit();
            _logger.LogInformation("Transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing transaction");
            throw;
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
            
            // Clear transaction from executor
            if (_executor is TransactionalStoredProcedureExecutor transactionalExecutor)
            {
                transactionalExecutor.ClearTransaction();
            }
        }

        await Task.CompletedTask;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No active transaction to rollback");
        }

        try
        {
            _logger.LogWarning("Rolling back transaction");
            _transaction.Rollback();
            _logger.LogWarning("Transaction rolled back");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back transaction");
            throw;
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
            
            // Clear transaction from executor
            if (_executor is TransactionalStoredProcedureExecutor transactionalExecutor)
            {
                transactionalExecutor.ClearTransaction();
            }
        }

        await Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Since we're using stored procedures, there's no need to track changes
        // All operations are executed immediately
        _logger.LogDebug("SaveChangesAsync called - no-op for stored procedure pattern");
        return Task.FromResult(0);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_transaction != null)
                {
                    try
                    {
                        _logger.LogWarning("Disposing UnitOfWork with active transaction - rolling back");
                        _transaction.Rollback();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error rolling back transaction during disposal");
                    }
                    finally
                    {
                        _transaction.Dispose();
                        _transaction = null;
                    }
                }

                _connection?.Dispose();
                _connection = null;
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Extension of StoredProcedureExecutor that supports transactions
/// </summary>
public class TransactionalStoredProcedureExecutor : StoredProcedureExecutor
{
    private IDbTransaction? _currentTransaction;

    public TransactionalStoredProcedureExecutor(
        IDatabaseConnectionFactory connectionFactory,
        ILogger<TransactionalStoredProcedureExecutor> logger) 
        : base(connectionFactory, logger)
    {
    }

    public void SetTransaction(IDbTransaction transaction)
    {
        _currentTransaction = transaction;
    }

    public void ClearTransaction()
    {
        _currentTransaction = null;
    }

    protected override async Task<SqlCommand> CreateCommandAsync(
        string storedProcedureName, 
        SqlParameter[] parameters, 
        CancellationToken cancellationToken)
    {
        if (_currentTransaction != null)
        {
            // Use the transaction's connection
            var command = new SqlCommand(storedProcedureName, (SqlConnection)_currentTransaction.Connection, (SqlTransaction)_currentTransaction)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };

            if (parameters?.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            return command;
        }

        // Fall back to base implementation
        return await base.CreateCommandAsync(storedProcedureName, parameters, cancellationToken);
    }
}