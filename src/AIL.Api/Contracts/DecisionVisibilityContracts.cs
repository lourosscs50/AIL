using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

// ---------------------------------------------------------------------------
// Core decision visibility shape — aligned with SignalForge.Contracts.Decisions
// (reference implementation: SignalForge repo, src/SignalForge.Contracts/Decisions/DecisionVisibilityContracts.cs).
// A.I.L. reuses the same record names and property order for the primary envelope
// so ControlPlane and other consumers can render decisions uniformly.
//
// Additive differences are limited to:
// - Optional <see cref="DecisionTraceSummary.ExecutionInstanceId"/> (A.I.L. invocation id; SignalForge trace uses null for this slot today).
// - Optional <see cref="DecisionVisibilityResponse.ExecutionExtension"/> (prompt/memory/reliability facets; not used by SignalForge).
// ---------------------------------------------------------------------------

public sealed record DecisionInputSummary(string NormalizedSummary);

public sealed record DecisionOutputSummary(string ResultSummary);

/// <summary>Bounded, operator-safe explanation surface (no hidden chain-of-thought).</summary>
public sealed record DecisionExplanationSummary(
    bool ExplanationAvailable,
    string? SummaryText,
    IReadOnlyList<string>? ReasonCodes,
    string? ConfidenceBand,
    int? FallbackUsageCount,
    int? RetryUsageCount);

/// <summary>
/// Trace and correlation handles for operator visibility.
/// <para><b>Alignment:</b> Same members and semantics as SignalForge <c>DecisionTraceSummary</c>.
/// <see cref="ExecutionId"/> is the SignalForge legacy alert-entity slot — A.I.L. sets it to null.</para>
/// <para><b>Additive:</b> <see cref="ExecutionInstanceId"/> carries the A.I.L. execution invocation id (distinct from <see cref="ExecutionId"/>).</para>
/// </summary>
public sealed record DecisionTraceSummary(
    Guid? CorrelationId,
    Guid? ExecutionId,
    string? TraceId,
    IReadOnlyList<Guid> RelatedEntityIds,
    Guid? SignalEntityId = null,
    Guid? AlertEntityId = null,
    Guid? ChronoFlowExecutionInstanceId = null,
    /// <summary>
    /// Additive (A.I.L.): stable execution invocation id. Do not map this into <see cref="ExecutionId"/> (reserved for SignalForge legacy meaning).
    /// </summary>
    Guid? ExecutionInstanceId = null);

/// <summary>
/// Operator-safe execution-only details. Omitted (null) for non–A.I.L. decision sources; ControlPlane may ignore when rendering generic decision cards.
/// </summary>
public sealed record ExecutionVisibilityExtension(
    ExecutionPromptFacet Prompt,
    ExecutionMemoryFacet Memory,
    ExecutionReliabilityFacet Reliability,
    string? SafeErrorSummary,
    DateTime StartedAtUtc);

/// <summary>Prompt registry facts (identifiers only; never template body).</summary>
public sealed record ExecutionPromptFacet(
    string PromptKey,
    string? PromptVersion,
    bool ResolutionSucceeded);

/// <summary>Memory participation facts (no raw payloads).</summary>
public sealed record ExecutionMemoryFacet(
    bool MemoryRequested,
    int? MemoryItemCount,
    string? ParticipationSummary);

/// <summary>Provider / fallback snapshot (observability only).</summary>
public sealed record ExecutionReliabilityFacet(
    bool FallbackUsed,
    string? PolicyKey,
    string? StrategyKey,
    string PrimaryProviderKey,
    string PrimaryModelKey,
    string? SelectedProviderKey,
    string? SelectedModelKey,
    string? FallbackProviderKey,
    string? FallbackModelKey);

/// <summary>
/// Unified decision visibility envelope — aligned with SignalForge <c>DecisionVisibilityResponse</c>.
/// For execute-intelligence, <see cref="DecisionId"/> equals the A.I.L. execution instance id used for <c>GET /executions/{{id}}</c>.
/// </summary>
public sealed record DecisionVisibilityResponse(
    Guid DecisionId,
    string DecisionCategory,
    string DecisionType,
    DateTimeOffset OccurredAtUtc,
    string Status,
    DecisionInputSummary Input,
    DecisionOutputSummary Output,
    string? PolicyProfileKey,
    string? StrategyPathKey,
    string? ProviderModelSummary,
    DecisionExplanationSummary Explanation,
    string? RecommendedDownstreamSummary,
    string? AuditActorUserId,
    DecisionTraceSummary Trace,
    /// <summary>Additive (A.I.L.): execution-specific facets; null when this row is not from execution observability.</summary>
    ExecutionVisibilityExtension? ExecutionExtension = null);

/// <summary>
/// Read-only paged list of execution decision visibility snapshots (newest completion time first).
/// JSON shape matches ControlPlane <c>PagedResult&lt;DecisionVisibilityResponse&gt;</c> (camelCase items).
/// </summary>
public sealed record PagedDecisionVisibilityResponse(
    IReadOnlyList<DecisionVisibilityResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
