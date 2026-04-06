using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Storage-agnostic persistence boundary for operator decision history snapshots (not audit, not raw telemetry).
/// Implementations may enforce bounded retention; <see cref="List"/> totals and <see cref="TryGet"/> reflect only retained rows.
/// </summary>
public interface IDecisionHistoryStore
{
    /// <summary>Upserts the record keyed by <see cref="DecisionHistoryRecord.Id"/>.</summary>
    void Put(DecisionHistoryRecord record);

    /// <summary>Returns the record only when <paramref name="decisionId"/> belongs to <paramref name="tenantId"/>.</summary>
    DecisionHistoryRecord? TryGet(Guid tenantId, Guid decisionId);

    /// <summary>Lists rows for <see cref="DecisionHistoryListQuery.TenantId"/> with bounded filters, sort, and paging.</summary>
    (IReadOnlyList<DecisionHistoryRecord> Items, int TotalCount) List(DecisionHistoryListQuery query);
}
