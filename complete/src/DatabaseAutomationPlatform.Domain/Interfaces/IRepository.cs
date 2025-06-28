using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Interfaces
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

        /// <summary>
        /// Get entities with pagination
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged result</returns>
        Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a paged result
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Items in this page
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
