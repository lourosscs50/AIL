using System;

namespace AIL.Modules.Observability.Application;

public sealed record DecisionTelemetry(
    Guid TenantId,
    string DecisionType,
    string SelectedStrategyKey,
    string ExecutionStage,
    bool UsedMemory,
    int MemoryItemCount,
    int CandidateStrategyCount,
    int ConsideredStrategyCount,
    long DurationMs,
    bool Succeeded,
    string? PolicyKey = null,
    string? ConfidenceTier = null,
    bool FallbackApplied = false,
    string? FailureCategory = null,
    string? ErrorMessage = null,
    string? MemoryInfluenceSummary = null);
