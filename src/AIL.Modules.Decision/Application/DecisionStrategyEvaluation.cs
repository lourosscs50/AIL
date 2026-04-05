namespace AIL.Modules.Decision.Application;

/// <summary>Deterministic evaluation output from a single strategy.</summary>
public sealed record DecisionStrategyEvaluation(
    int DeterministicScore,
    string SuggestedStrategyKey,
    string ReasonSummary);
