using System;

namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>
/// Single read model for execution observability (API, telemetry alignment, optional GET store).
/// </summary>
public sealed record ExecutionVisibilityReadModel(
    string CapabilityKey,
    ExecutionTraceVisibility Trace,
    ExecutionPromptVisibility Prompt,
    ExecutionMemoryVisibility Memory,
    ExecutionReliabilityVisibility Reliability,
    ExecutionExplanationVisibility Explanation,
    string OutputSummary,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string Status,
    string? SafeErrorSummary);
