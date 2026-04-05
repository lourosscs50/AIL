namespace AIL.Modules.Decision.Domain;

/// <summary>Stable keys for built-in deterministic strategies (not product-specific).</summary>
public static class KnownDecisionStrategyKeys
{
    public const string DefaultSafe = "default_safe";
    public const string ContextEscalated = "context_escalated";
    public const string MemoryInformed = "memory_informed";
    public const string CandidateMatch = "candidate_match";
    public const string DecisionContinuity = "decision_continuity";
}
