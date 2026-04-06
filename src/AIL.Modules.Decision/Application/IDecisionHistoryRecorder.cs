using System;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Persists a safe history snapshot after a successful <see cref="IDecisionService.DecideAsync"/> outcome.
/// </summary>
public interface IDecisionHistoryRecorder
{
    /// <summary>
    /// Returns the new history record id, or <c>null</c> if persistence failed after the decision was computed (decision result remains valid; <c>null</c> means no durable id — not a claim that history was written).
    /// </summary>
    Guid? TryRecord(DecisionRequest request, DecisionResult result);
}
