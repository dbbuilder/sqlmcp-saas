using DatabaseAutomationPlatform.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Interfaces;

/// <summary>
/// Unit of Work pattern interface for managing transactions across multiple repositories
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Audit event repository
    /// </summary>
    IAuditEventRepository AuditEvents { get; }

    /// <summary>
    /// Database task repository
    /// </summary>
    IDatabaseTaskRepository DatabaseTasks { get; }

    /// <summary>
    /// Data classification repository
    /// </summary>
    IDataClassificationRepository DataClassifications { get; }

    /// <summary>
    /// Begin a new transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save all changes (for implementations that batch operations)
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a transaction is active
    /// </summary>
    bool HasActiveTransaction { get; }
}