using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class CandidateMatchDecisionStrategy : IDecisionStrategy
{
    public string StrategyKey => KnownDecisionStrategyKeys.CandidateMatch;

    public bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory) =>
        request.CandidateStrategies is { Count: 1 } &&
        !string.IsNullOrWhiteSpace(request.CandidateStrategies[0]);

    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory)
    {
        var key = request.CandidateStrategies!.Single().Trim();
        return new DecisionStrategyEvaluation(
            DeterministicScore: 1000,
            SuggestedStrategyKey: key,
            ReasonSummary: "single_candidate_match");
    }
}
