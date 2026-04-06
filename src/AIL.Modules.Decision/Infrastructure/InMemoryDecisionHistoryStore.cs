using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Bounded in-process store for operator decision history (same deployment profile as execution visibility snapshots).
/// </summary>
internal sealed class InMemoryDecisionHistoryStore : IDecisionHistoryStore
{
    private const int MaxEntries = 512;
    private const int MaxPageSize = 100;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, DecisionHistoryRecord> _byId = new();
    private readonly Queue<Guid> _insertionOrder = new();

    public void Put(DecisionHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var id = record.Id;
        lock (_lock)
        {
            var isNew = !_byId.ContainsKey(id);
            if (isNew)
            {
                while (_byId.Count >= MaxEntries && _insertionOrder.Count > 0)
                {
                    var evict = _insertionOrder.Dequeue();
                    _byId.Remove(evict);
                }

                _insertionOrder.Enqueue(id);
            }

            _byId[id] = record;
        }
    }

    public DecisionHistoryRecord? TryGet(Guid tenantId, Guid decisionId)
    {
        lock (_lock)
        {
            if (!_byId.TryGetValue(decisionId, out var r))
                return null;
            return r.TenantId == tenantId ? r : null;
        }
    }

    public (IReadOnlyList<DecisionHistoryRecord> Items, int TotalCount) List(DecisionHistoryListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(query));

        var p = query.Page < 1 ? 1 : query.Page;
        var ps = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, MaxPageSize);

        lock (_lock)
        {
            IEnumerable<DecisionHistoryRecord> rows = _byId.Values.Where(r => r.TenantId == query.TenantId);

            if (!string.IsNullOrWhiteSpace(query.DecisionType))
                rows = rows.Where(r => string.Equals(r.DecisionType, query.DecisionType, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(query.SelectedStrategyKey))
                rows = rows.Where(r => string.Equals(r.SelectedStrategyKey, query.SelectedStrategyKey, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(query.PolicyKey))
                rows = rows.Where(r => string.Equals(r.PolicyKey, query.PolicyKey, StringComparison.Ordinal));

            if (query.CreatedFromUtc is DateTime from)
                rows = rows.Where(r => r.CreatedAtUtc >= from);

            if (query.CreatedToUtc is DateTime to)
                rows = rows.Where(r => r.CreatedAtUtc <= to);

            if (query.CorrelationGroupId is { } correlationId)
                rows = rows.Where(r => r.CorrelationGroupId == correlationId);

            if (query.ExecutionInstanceId is { } executionId)
                rows = rows.Where(r => r.ExecutionInstanceId == executionId);

            if (!string.IsNullOrWhiteSpace(query.MemoryInfluenceSummary))
                rows = rows.Where(r =>
                    string.Equals(
                        r.MemoryInfluenceSummary,
                        query.MemoryInfluenceSummary,
                        StringComparison.Ordinal));

            var ordered = rows
                .OrderByDescending(r => r.CreatedAtUtc)
                .ThenBy(r => r.Id)
                .ToList();

            var total = ordered.Count;
            var skip = (p - 1) * ps;
            var slice = skip >= ordered.Count
                ? new List<DecisionHistoryRecord>()
                : ordered.Skip(skip).Take(ps).ToList();

            return (slice, total);
        }
    }
}
