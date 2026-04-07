using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// SQLite-backed <see cref="IDecisionHistoryStore"/>. Persists only fields on <see cref="DecisionHistoryRecord"/> (operator-safe snapshot).
/// Unlike <see cref="InMemoryDecisionHistoryStore"/>, this implementation does <b>not</b> apply <see cref="DecisionHistoryRetentionOptions.MaxRetainedRecords"/>;
/// retention is unbounded at the store layer (use external lifecycle policies if needed).
/// Pending migrations are applied eagerly when the API host runs (<see cref="DecisionHistoryStoreReadinessHostedService"/>); otherwise the first call to this store
/// triggers the same migration path (for example in tests that do not start hosted services).
/// There is no fallback to <see cref="InMemoryDecisionHistoryStore"/> inside this implementation: if SQLite configuration or initialization is invalid, operations fail.
/// </summary>
internal sealed class EfDecisionHistoryStore : IDecisionHistoryStore
{
    private const int MaxPageSize = 100;
    private readonly IDbContextFactory<DecisionHistoryDbContext> _factory;
    private readonly object _initLock = new();
    private bool _initialized;

    public EfDecisionHistoryStore(IDbContextFactory<DecisionHistoryDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    private void EnsureDatabase()
    {
        if (_initialized)
            return;
        lock (_initLock)
        {
            if (_initialized)
                return;
            DecisionHistoryDatabaseInitializer.EnsureReady(_factory);
            _initialized = true;
        }
    }

    public void Put(DecisionHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureDatabase();
        using var db = _factory.CreateDbContext();
        var existing = db.DecisionHistory.Find(record.Id);
        if (existing is null)
            db.DecisionHistory.Add(DecisionHistoryRecordMapper.ToEntity(record));
        else
            DecisionHistoryRecordMapper.UpdateEntity(existing, record);
        db.SaveChanges();
    }

    public DecisionHistoryRecord? TryGet(Guid tenantId, Guid decisionId)
    {
        EnsureDatabase();
        using var db = _factory.CreateDbContext();
        var row = db.DecisionHistory.AsNoTracking().FirstOrDefault(e => e.Id == decisionId);
        if (row is null)
            return null;
        return row.TenantId == tenantId ? DecisionHistoryRecordMapper.ToRecord(row) : null;
    }

    public (IReadOnlyList<DecisionHistoryRecord> Items, int TotalCount) List(DecisionHistoryListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(query));

        var p = query.Page < 1 ? 1 : query.Page;
        var ps = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, MaxPageSize);

        EnsureDatabase();
        using var db = _factory.CreateDbContext();
        IQueryable<DecisionHistoryEntity> rows = db.DecisionHistory.AsNoTracking()
            .Where(r => r.TenantId == query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.DecisionType))
        {
            var dt = query.DecisionType;
            rows = rows.Where(r => r.DecisionType == dt);
        }

        if (!string.IsNullOrWhiteSpace(query.SelectedStrategyKey))
        {
            var sk = query.SelectedStrategyKey;
            rows = rows.Where(r => r.SelectedStrategyKey == sk);
        }

        if (!string.IsNullOrWhiteSpace(query.PolicyKey))
        {
            var pk = query.PolicyKey;
            rows = rows.Where(r => r.PolicyKey == pk);
        }

        if (query.CreatedFromUtc is DateTime from)
            rows = rows.Where(r => r.CreatedAtUtc >= from);

        if (query.CreatedToUtc is DateTime to)
            rows = rows.Where(r => r.CreatedAtUtc <= to);

        if (query.CorrelationGroupId is { } correlationId)
            rows = rows.Where(r => r.CorrelationGroupId == correlationId);

        if (query.ExecutionInstanceId is { } executionId)
            rows = rows.Where(r => r.ExecutionInstanceId == executionId);

        if (!string.IsNullOrWhiteSpace(query.MemoryInfluenceSummary))
        {
            var mis = query.MemoryInfluenceSummary;
            rows = rows.Where(r => r.MemoryInfluenceSummary == mis);
        }

        IOrderedQueryable<DecisionHistoryEntity> ordered = query.SortBy switch
        {
            DecisionHistorySortBy.CreatedAtUtc => query.SortDirection == DecisionHistorySortDirection.Descending
                ? rows.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id)
                : rows.OrderBy(r => r.CreatedAtUtc).ThenBy(r => r.Id),
            _ => throw new ArgumentException($"Unsupported SortBy: {query.SortBy}.", nameof(query)),
        };

        var total = ordered.Count();
        var entities = ordered
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToList();
        var slice = entities.Select(DecisionHistoryRecordMapper.ToRecord).ToList();

        return (slice, total);
    }
}
