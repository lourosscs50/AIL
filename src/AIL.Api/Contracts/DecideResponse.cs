using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

/// <summary>
/// Bounded, operator-safe advisory decision response. Exposes only decision truth A.I.L. derives from its pipeline
/// (strategy selection, confidence tier, high-level rationale, policy key). Does not include prompts, raw provider
/// payloads, memory record bodies, or hidden reasoning traces.
/// <see cref="SelectedOptionId"/> is set when it matches an <see cref="DecideOptionResponse.OptionId"/> in <see cref="Options"/>; otherwise <c>null</c>.
/// <see cref="PolicyKey"/> is the resolved policy key for <see cref="DecisionType"/> (aligned with observability telemetry).
/// <see cref="Metadata"/> is always <c>null</c> on this public surface—client-supplied request metadata is not echoed.
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
    string PolicyKey,
    IReadOnlyDictionary<string, string>? Metadata,
    string? SelectedOptionId);
