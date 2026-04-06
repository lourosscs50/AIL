using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Process-local decision history until a durable shared store exists. Not audit and not raw telemetry.
/// </summary>
public interface IDecisionHistoryStore
{
    void Put(DecisionHistoryRecord record);

    /// <summary>Returns the record only when <paramref name="decisionId"/> belongs to <paramref name="tenantId"/>.</summary>
    DecisionHistoryRecord? TryGet(Guid tenantId, Guid decisionId);

    (IReadOnlyList<DecisionHistoryRecord> Items, int TotalCount) List(DecisionHistoryListQuery query);
}
