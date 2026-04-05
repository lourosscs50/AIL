using System.Collections.Generic;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionResult(
    string DecisionType,
    string SelectedStrategyKey,
    DecisionConfidence Confidence,
    string ReasonSummary,
    IReadOnlyList<string> ConsideredStrategies,
    bool UsedMemory,
    int MemoryItemCount,
    IReadOnlyList<DecisionOption> Options,
    IReadOnlyDictionary<string, string>? Metadata);
