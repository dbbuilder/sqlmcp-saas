using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlServerMcpFunctions.Domain.Interfaces
{
    /// <summary>
    /// Base repository interface for data access operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <typeparam name="TKey">Primary key type</typeparam>
    public interface IRepository<T, TKey> where T : class
    {
        /// <summary>
        /// Get entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entity if found, null otherwise</returns>
        Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all entities
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of all entities</returns>
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Add a new entity
        /// </summary>
        /// <param name="entity">Entity to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Added entity with generated keys</returns>
        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
        /// <summary>
        /// Update an existing entity
        /// </summary>
        /// <param name="entity">Entity to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated entity</returns>
        Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete an entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if entity exists by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if exists, false otherwise</returns>
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
    }
}
