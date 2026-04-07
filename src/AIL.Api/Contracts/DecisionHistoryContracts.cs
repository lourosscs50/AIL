using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

/// <summary>
/// One option row in a persisted decision history response (operator-safe; no model payloads).
/// </summary>
public sealed record DecisionHistoryOptionItemResponse(
    string OptionId,
    string ConfidenceTier,
    double Strength,
    string RationaleSummary);

/// <summary>
/// Compact operator-safe row for <c>GET /decisions/history</c> list responses. Excludes rationale text, per-option rows,
/// and considered-strategy lists—use <see cref="DecisionHistoryItemResponse"/> from <c>GET /decisions/history/{{id}}</c> for those.
/// </summary>
public sealed record DecisionHistoryListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid? CorrelationGroupId,
    Guid? ExecutionInstanceId,
    string DecisionType,
    string SubjectType,
    string SubjectId,
    string SelectedStrategyKey,
    string? SelectedOptionId,
    string ConfidenceTier,
    string PolicyKey,
    bool UsedMemory,
    int MemoryItemCount,
    string MemoryInfluenceSummary,
    string Outcome,
    DateTime CreatedAtUtc);

/// <summary>
/// Full operator-safe decision history detail (durable snapshot). Distinct from execution visibility and audit.
/// </summary>
public sealed record DecisionHistoryItemResponse(
    Guid Id,
    Guid TenantId,
    Guid? CorrelationGroupId,
    Guid? ExecutionInstanceId,
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
    IReadOnlyList<DecisionHistoryOptionItemResponse> Options,
    string Outcome,
    DateTime CreatedAtUtc);

/// <summary>
/// Paged list of compact history rows. <see cref="SortBy"/> and <see cref="SortDirection"/> echo the applied bounded sort (default: <c>createdAtUtc</c> descending).
/// Paging: omitted query parameters default to page <c>1</c> and pageSize <c>50</c>; explicit <c>page</c> must be ≥ <c>1</c>, explicit <c>pageSize</c> must be between <c>1</c> and <c>100</c> (see <see cref="AIL.Api.DecisionEndpointMapping"/> constants).
/// </summary>
public sealed record PagedDecisionHistoryResponse(
    IReadOnlyList<DecisionHistoryListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string SortBy,
    string SortDirection);
