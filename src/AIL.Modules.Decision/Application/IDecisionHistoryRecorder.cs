using System;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Persists a safe history snapshot after a successful <see cref="IDecisionService.DecideAsync"/> outcome.
/// </summary>
public interface IDecisionHistoryRecorder
{
    /// <summary>
    /// Returns the new history record id, or <c>null</c> if persistence failed (decision result remains valid).
    /// </summary>
    Guid? TryRecord(DecisionRequest request, DecisionResult result);
}
