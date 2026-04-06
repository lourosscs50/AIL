using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Operator-safe, platform-neutral decision history row. Distinct from audit trails and runtime telemetry.
/// Does not include prompts, raw model output, structured context bodies, or request metadata.
/// </summary>
public sealed record DecisionHistoryRecord(
    Guid Id,
    Guid TenantId,
    Guid? CorrelationGroupId,
    string DecisionType,
    string SubjectType,
    string SubjectId,
    string SelectedStrategyKey,
    string? SelectedOptionId,
    string ConfidenceTier,
    string PolicyKey,
    string ReasonSummary,
    IReadOnlyList<string> ConsideredStrategies,
    bool UsedMemory,
    int MemoryItemCount,
    string MemoryInfluenceSummary,
    IReadOnlyList<DecisionHistoryOptionSnapshot> Options,
    string Outcome,
    DateTime CreatedAtUtc);
