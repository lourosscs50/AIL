using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure.Strategies;

internal sealed class DecisionContinuityStrategy : IDecisionStrategy
{
    public string StrategyKey => KnownDecisionStrategyKeys.DecisionContinuity;

    public bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory)
    {
        if (memory is null || memory.Items.Count == 0 || !request.IncludeMemory)
            return false;

        // Check if any memory item has decision_type metadata matching the request
        return memory.Items.Any(item =>
            item.Metadata != null &&
            item.Metadata.TryGetValue("decision_type", out var decisionType) &&
            decisionType == request.DecisionType &&
            !string.IsNullOrWhiteSpace(item.Content) &&
            request.CandidateStrategies?.Contains(item.Content) == true);
    }

    public DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory)
    {
        if (memory is null || memory.Items.Count == 0)
            return new DecisionStrategyEvaluation(
                DeterministicScore: 0,
                SuggestedStrategyKey: KnownDecisionStrategyKeys.DefaultSafe,
                ReasonSummary: "no_memory_context");

        // Find the most recent compatible historical choice
        var historicalChoice = memory.Items
            .Where(item =>
                item.Metadata != null &&
                item.Metadata.TryGetValue("decision_type", out var decisionType) &&
                decisionType == request.DecisionType &&
                !string.IsNullOrWhiteSpace(item.Content) &&
                request.CandidateStrategies?.Contains(item.Content) == true)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.Content)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(historicalChoice))
            return new DecisionStrategyEvaluation(
                DeterministicScore: 0,
                SuggestedStrategyKey: KnownDecisionStrategyKeys.DefaultSafe,
                ReasonSummary: "no_qualifying_history");

        return new DecisionStrategyEvaluation(
            DeterministicScore: 100, // Capped weak signal
            SuggestedStrategyKey: historicalChoice,
            ReasonSummary: "continuity_from_historical_choice");
    }
}