using System.Collections.Generic;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class ContextEscalatedDecisionStrategy : IDecisionStrategy
{
    public string StrategyKey => KnownDecisionStrategyKeys.ContextEscalated;

    public bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory)
    {
        var inputs = Normalize(request.StructuredContext);
        return inputs.IsEscalated();
    }

    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory) =>
        new(
            DeterministicScore: 900,
            SuggestedStrategyKey: KnownDecisionStrategyKeys.ContextEscalated,
            ReasonSummary: "structured_escalation_or_high_priority");

    private static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? raw) =>
        raw ?? new Dictionary<string, string>();
}
