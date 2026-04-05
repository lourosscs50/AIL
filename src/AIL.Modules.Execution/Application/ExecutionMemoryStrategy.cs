namespace AIL.Modules.Execution.Application;

/// <summary>
/// Context information available to memory strategies for making decisions.
/// </summary>
public sealed record ExecutionMemoryStrategyContext(
    string CapabilityKey,
    ExecutionMemoryQuery? MemoryQuery,
    bool HasExplicitRequestOverride,
    bool HasCapabilityDefault);

/// <summary>
/// Decision result from a memory strategy.
/// </summary>
public sealed record ExecutionMemoryStrategyDecision(
    bool ShouldUseMemory,
    int? SuggestedMaxResults = null,
    string DecisionReason = "");