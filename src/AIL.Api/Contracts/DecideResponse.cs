using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record DecideResponse(
    string DecisionType,
    string SelectedStrategyKey,
    string Confidence,
    string ReasonSummary,
    IReadOnlyList<DecideOptionResponse> Options,
    IReadOnlyList<string> ConsideredStrategies,
    bool UsedMemory,
    int MemoryItemCount,
    IReadOnlyDictionary<string, string>? Metadata);
