using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Domain.Repositories;

/// <summary>
/// Repository interface for audit event operations
/// </summary>
public interface IAuditEventRepository : IRepository<AuditEvent, long>
{
    /// <summary>
    /// Get audit events by user
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetByUserAsync(string userId, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit events by date range
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit events by entity
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit events by action
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetByActionAsync(string action, DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search audit events
    /// </summary>
    Task<PagedResult<AuditEvent>> SearchAsync(AuditSearchCriteria criteria, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old audit events
    /// </summary>
    Task<int> CleanupOldEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for audit events
/// </summary>
public class AuditSearchCriteria
{
    public string? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public AuditSeverity? Severity { get; set; }
    public bool? Success { get; set; }
    public string? SearchText { get; set; }
}