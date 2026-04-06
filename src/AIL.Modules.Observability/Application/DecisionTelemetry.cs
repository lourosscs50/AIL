using System;

namespace AIL.Modules.Observability.Application;

public sealed record DecisionTelemetry(
    Guid TenantId,
    string DecisionType,
    string SelectedStrategyKey,
    bool UsedMemory,
    int MemoryItemCount,
    int CandidateStrategyCount,
    int ConsideredStrategyCount,
    long DurationMs,
    bool Succeeded,
    string? PolicyKey = null,
    string? ErrorMessage = null,
    string? MemoryInfluenceSummary = null);
