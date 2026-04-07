using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Storage-agnostic persistence boundary for operator decision history snapshots (not audit, not raw telemetry).
/// This abstraction intentionally permits different lifecycle profiles:
/// durable implementations can survive process restarts, while in-memory implementations are process-scoped and retention-bounded.
/// <see cref="List"/> totals and <see cref="TryGet"/> always reflect only rows currently retained by the concrete store.
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
