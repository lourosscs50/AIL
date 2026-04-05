namespace AIL.Modules.Decision.Application;

public interface IDecisionStrategy
{
    /// <summary>Registry key for this strategy (e.g. candidate_match).</summary>
    string StrategyKey { get; }

    bool CanHandle(DecisionRequest request, DecisionMemoryContext? memory);

    /// <summary>SynCHRONOUS deterministic evaluation when <see cref="CanHandle"/> is true.</summary>
    DecisionStrategyEvaluation Evaluate(DecisionRequest request, DecisionMemoryContext? memory);
}
