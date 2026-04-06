using System.Collections.Generic;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Application;

/// <param name="PolicyKey">Resolved policy key for this decision type (same truth as observability telemetry).</param>
public sealed record DecisionResult(
    string DecisionType,
    string SelectedStrategyKey,
    DecisionConfidence Confidence,
    string ReasonSummary,
    IReadOnlyList<string> ConsideredStrategies,
    bool UsedMemory,
    int MemoryItemCount,
    /// <summary>Bounded label from <c>KnownMemoryInfluenceSummaries</c>; never contains memory content.</summary>
    string MemoryInfluenceSummary,
    IReadOnlyList<DecisionOption> Options,
    string PolicyKey,
    IReadOnlyDictionary<string, string>? Metadata);
