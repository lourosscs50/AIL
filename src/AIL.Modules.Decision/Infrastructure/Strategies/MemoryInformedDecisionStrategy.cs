using System.Collections.Generic;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class MemoryInformedDecisionStrategy : IDecisionStrategy
{
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

    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory) =>
        new(
            DeterministicScore: 800,
            SuggestedStrategyKey: KnownDecisionStrategyKeys.MemoryInformed,
            ReasonSummary: "memory_items_present_with_context_signal");

    private static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? raw) =>
        raw ?? new Dictionary<string, string>();
}
