using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using DatabaseAutomationPlatform.Infrastructure.Data;
using DatabaseAutomationPlatform.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DatabaseAutomationPlatform.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering repository services
/// </summary>
public static class RepositoryServiceExtensions
{
    /// <summary>
    /// Add repository services to the DI container
    /// </summary>
    public static IServiceCollection AddRepositoryServices(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // Register repositories
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IDatabaseTaskRepository, DatabaseTaskRepository>();
        services.AddScoped<IDataClassificationRepository, DataClassificationRepository>();

        // Register Unit of Work
        services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped<IScopedUnitOfWork, ScopedUnitOfWork>();
        
        // Register IUnitOfWork to resolve to IScopedUnitOfWork
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IScopedUnitOfWork>());

        // Register transactional stored procedure executor
        services.AddScoped<TransactionalStoredProcedureExecutor>();

        return services;
    }
}