using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Infrastructure.Data;

/// <summary>
/// Factory implementation for creating Unit of Work instances
/// </summary>
public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UnitOfWorkFactory> _logger;

    public UnitOfWorkFactory(IServiceProvider serviceProvider, ILogger<UnitOfWorkFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IUnitOfWork Create()
    {
        _logger.LogDebug("Creating new Unit of Work instance");

        var connectionFactory = _serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
        var executor = _serviceProvider.GetRequiredService<IStoredProcedureExecutor>();
        var unitOfWorkLogger = _serviceProvider.GetRequiredService<ILogger<UnitOfWork>>();

        // Create a transactional executor if the registered executor supports it
        if (executor is TransactionalStoredProcedureExecutor)
        {
            return new UnitOfWork(connectionFactory, executor, unitOfWorkLogger);
        }

        // Otherwise create a new transactional executor
        var transactionalExecutorLogger = _serviceProvider.GetRequiredService<ILogger<TransactionalStoredProcedureExecutor>>();
        var transactionalExecutor = new TransactionalStoredProcedureExecutor(connectionFactory, transactionalExecutorLogger);
        
        return new UnitOfWork(connectionFactory, transactionalExecutor, unitOfWorkLogger);
    }
}

/// <summary>
/// Scoped Unit of Work implementation for dependency injection
/// </summary>
public class ScopedUnitOfWork : IScopedUnitOfWork
{
    private readonly IUnitOfWork _unitOfWork;
    private bool _disposed;

    public ScopedUnitOfWork(IUnitOfWorkFactory factory)
    {
        _unitOfWork = factory?.Create() ?? throw new ArgumentNullException(nameof(factory));
    }

    public IUnitOfWork UnitOfWork => _unitOfWork;

    public IAuditEventRepository AuditEvents => _unitOfWork.AuditEvents;
    public IDatabaseTaskRepository DatabaseTasks => _unitOfWork.DatabaseTasks;
    public IDataClassificationRepository DataClassifications => _unitOfWork.DataClassifications;
    public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.BeginTransactionAsync(cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.RollbackAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.SaveChangesAsync(cancellationToken);

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
                _unitOfWork?.Dispose();
            }
            _disposed = true;
        }
    }
}