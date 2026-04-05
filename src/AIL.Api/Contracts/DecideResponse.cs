using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

/// <summary>
/// Advisory decision response. Options remain the canonical option payload.
/// <see cref="SelectedOptionId"/> is the explicit winning option identifier when it matches
/// an <see cref="DecideOptionResponse.OptionId"/> in <see cref="Options"/>; otherwise <c>null</c>.
/// </summary>
public sealed record DecideResponse(
    string DecisionType,
    string SelectedStrategyKey,
    string Confidence,
    string ReasonSummary,
    IReadOnlyList<DecideOptionResponse> Options,
    IReadOnlyList<string> ConsideredStrategies,
    bool UsedMemory,
    int MemoryItemCount,
    IReadOnlyDictionary<string, string>? Metadata,
    string? SelectedOptionId);
