using System;

namespace DatabaseAutomationPlatform.Domain.Interfaces;

/// <summary>
/// Factory for creating Unit of Work instances
/// </summary>
public interface IUnitOfWorkFactory
{
    /// <summary>
    /// Create a new Unit of Work instance
    /// </summary>
    IUnitOfWork Create();
}

/// <summary>
/// Scoped Unit of Work that automatically disposes
/// </summary>
public interface IScopedUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// The underlying Unit of Work instance
    /// </summary>
    IUnitOfWork UnitOfWork { get; }
}