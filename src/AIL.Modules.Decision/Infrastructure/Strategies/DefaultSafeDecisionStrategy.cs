using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class DefaultSafeDecisionStrategy : IDecisionStrategy
{
    public string StrategyKey => KnownDecisionStrategyKeys.DefaultSafe;

    public bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory) => true;

    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory) =>
        new(
            DeterministicScore: 100,
            SuggestedStrategyKey: KnownDecisionStrategyKeys.DefaultSafe,
            ReasonSummary: "default_safe_fallback");
}
