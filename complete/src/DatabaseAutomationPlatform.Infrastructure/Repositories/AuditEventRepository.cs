using DatabaseAutomationPlatform.Domain.Entities;
using DatabaseAutomationPlatform.Domain.Interfaces;
using DatabaseAutomationPlatform.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAutomationPlatform.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for audit events
/// </summary>
public class AuditEventRepository : BaseRepository<AuditEvent, long>, IAuditEventRepository
{
    public AuditEventRepository(IStoredProcedureExecutor executor, ILogger<AuditEventRepository> logger)
        : base(executor, logger, "AuditEvent")
    {
    }

    public async Task<IEnumerable<AuditEvent>> GetByUserAsync(string userId, int limit = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting audit events for user: {UserId}, Limit: {Limit}", userId, limit);

        var parameters = new[]
        {
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit)
        };

        var result = await _executor.ExecuteAsync("sp_AuditEvent_GetByUser", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<AuditEvent>> GetByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting audit events between {StartDate} and {EndDate}", startDate, endDate);

        var parameters = new[]
        {
            new SqlParameter("@StartDate", startDate),
            new SqlParameter("@EndDate", endDate)
        };

        var result = await _executor.ExecuteAsync("sp_AuditEvent_GetByDateRange", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<AuditEvent>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting audit events for entity: {EntityType}/{EntityId}", entityType, entityId);

        var parameters = new[]
        {
            new SqlParameter("@EntityType", entityType),
            new SqlParameter("@EntityId", entityId)
        };

        var result = await _executor.ExecuteAsync("sp_AuditEvent_GetByEntity", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<IEnumerable<AuditEvent>> GetByActionAsync(string action, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting audit events for action: {Action}, Since: {Since}", action, since);

        var parameters = new[]
        {
            new SqlParameter("@Action", action),
            new SqlParameter("@Since", since ?? SqlDateTime.MinValue)
        };

        var result = await _executor.ExecuteAsync("sp_AuditEvent_GetByAction", parameters, cancellationToken);
        return result.Rows.Cast<DataRow>().Select(MapFromDataRow);
    }

    public async Task<PagedResult<AuditEvent>> SearchAsync(AuditSearchCriteria criteria, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching audit events with criteria: {@Criteria}", criteria);

        var parameters = new[]
        {
            new SqlParameter("@UserId", (object?)criteria.UserId ?? DBNull.Value),
            new SqlParameter("@EntityType", (object?)criteria.EntityType ?? DBNull.Value),
            new SqlParameter("@EntityId", (object?)criteria.EntityId ?? DBNull.Value),
            new SqlParameter("@Action", (object?)criteria.Action ?? DBNull.Value),
            new SqlParameter("@StartDate", (object?)criteria.StartDate ?? DBNull.Value),
            new SqlParameter("@EndDate", (object?)criteria.EndDate ?? DBNull.Value),
            new SqlParameter("@Severity", (object?)criteria.Severity ?? DBNull.Value),
            new SqlParameter("@Success", (object?)criteria.Success ?? DBNull.Value),
            new SqlParameter("@SearchText", (object?)criteria.SearchText ?? DBNull.Value),
            new SqlParameter("@PageNumber", pageNumber),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@TotalCount", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
        };

        var result = await _executor.ExecuteAsync("sp_AuditEvent_Search", parameters, cancellationToken);
        var items = result.Rows.Cast<DataRow>().Select(MapFromDataRow).ToList();
        var totalCount = (long)parameters[11].Value;

        return new PagedResult<AuditEvent>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<int> CleanupOldEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cleaning up audit events older than {OlderThan}", olderThan);

        var parameters = new[]
        {
            new SqlParameter("@OlderThan", olderThan),
            new SqlParameter("@DeletedCount", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        await _executor.ExecuteNonQueryAsync("sp_AuditEvent_Cleanup", parameters, cancellationToken);
        
        var deletedCount = (int)parameters[1].Value;
        _logger.LogWarning("Deleted {DeletedCount} old audit events", deletedCount);

        return deletedCount;
    }

    protected override AuditEvent MapFromDataRow(DataRow row)
    {
        return new AuditEvent
        {
            Id = row.Field<long>("Id"),
            EventTime = row.Field<DateTimeOffset>("EventTime"),
            UserId = row.Field<string>("UserId") ?? string.Empty,
            UserName = row.Field<string>("UserName") ?? string.Empty,
            Action = row.Field<string>("Action") ?? string.Empty,
            EntityType = row.Field<string>("EntityType") ?? string.Empty,
            EntityId = row.Field<string>("EntityId") ?? string.Empty,
            OldValues = row.Field<string>("OldValues"),
            NewValues = row.Field<string>("NewValues"),
            IpAddress = row.Field<string>("IpAddress"),
            UserAgent = row.Field<string>("UserAgent"),
            CorrelationId = row.Field<string>("CorrelationId"),
            Success = row.Field<bool>("Success"),
            ErrorMessage = row.Field<string>("ErrorMessage"),
            Duration = row.Field<int?>("Duration"),
            AdditionalData = row.Field<string>("AdditionalData"),
            Severity = Enum.Parse<AuditSeverity>(row.Field<string>("Severity") ?? "Information")
        };
    }

    protected override SqlParameter[] GetInsertParameters(AuditEvent entity)
    {
        return new[]
        {
            new SqlParameter("@EventTime", entity.EventTime),
            new SqlParameter("@UserId", entity.UserId),
            new SqlParameter("@UserName", entity.UserName),
            new SqlParameter("@Action", entity.Action),
            new SqlParameter("@EntityType", entity.EntityType),
            new SqlParameter("@EntityId", entity.EntityId),
            new SqlParameter("@OldValues", (object?)entity.OldValues ?? DBNull.Value),
            new SqlParameter("@NewValues", (object?)entity.NewValues ?? DBNull.Value),
            new SqlParameter("@IpAddress", (object?)entity.IpAddress ?? DBNull.Value),
            new SqlParameter("@UserAgent", (object?)entity.UserAgent ?? DBNull.Value),
            new SqlParameter("@CorrelationId", (object?)entity.CorrelationId ?? DBNull.Value),
            new SqlParameter("@Success", entity.Success),
            new SqlParameter("@ErrorMessage", (object?)entity.ErrorMessage ?? DBNull.Value),
            new SqlParameter("@Duration", (object?)entity.Duration ?? DBNull.Value),
            new SqlParameter("@AdditionalData", (object?)entity.AdditionalData ?? DBNull.Value),
            new SqlParameter("@Severity", entity.Severity.ToString())
        };
    }

    protected override SqlParameter[] GetUpdateParameters(AuditEvent entity)
    {
        // Audit events are immutable - they should not be updated
        throw new NotSupportedException("Audit events cannot be updated");
    }

    public override Task<AuditEvent> UpdateAsync(AuditEvent entity, CancellationToken cancellationToken = default)
    {
        // Audit events are immutable
        throw new NotSupportedException("Audit events cannot be updated");
    }
}