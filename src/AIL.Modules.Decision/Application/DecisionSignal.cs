namespace AIL.Modules.Decision.Application;

/// <summary>
/// Closed set of categorized signals that influence decision explanation generation.
/// </summary>
public enum DecisionSignalType
{
    CandidateMatch,
    EscalationSignal,
    MemoryContext,
    HistoricalContinuity,
    DefaultFallback,
    Unknown
}

/// <summary>
/// Represents a single signal used to compose a standardized decision explanation.
/// </summary>
public sealed record DecisionSignal(
    DecisionSignalType Type,
    int Strength,
    bool IsActive);
