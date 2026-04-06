using System;
using System.Collections.Generic;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class MemoryInformedDecisionStrategy : IDecisionStrategy
{
    private const int BaseMemoryInformedScore = 800;
    private const int MaxItemsForScoreBoost = 20;
    private const int PointsPerBoundedItem = 2;

    public string StrategyKey => KnownDecisionStrategyKeys.MemoryInformed;

    public bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory)
    {
        if (memory is null || memory.Items.Count == 0)
            return false;

        var inputs = Normalize(request.StructuredContext);
        if (inputs.IsContextSensitive())
            return true;

        return !string.IsNullOrWhiteSpace(request.ContextText);
    }

    /// <summary>
    /// Score uses only the bounded item count (capped)—never raw memory text—for a deterministic tie-break within the memory-informed band.
    /// </summary>
    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory)
    {
        var n = memory?.Items.Count ?? 0;
        var bounded = Math.Min(n, MaxItemsForScoreBoost);
        var score = BaseMemoryInformedScore + bounded * PointsPerBoundedItem;
        return new DecisionStrategyEvaluation(
            DeterministicScore: score,
            SuggestedStrategyKey: KnownDecisionStrategyKeys.MemoryInformed,
            ReasonSummary: "memory_items_present_with_context_signal");
    }

    private static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? raw) =>
        raw ?? new Dictionary<string, string>();
}
