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
/// Operator-safe decision history item (durable snapshot). Distinct from execution visibility and audit.
/// </summary>
public sealed record DecisionHistoryItemResponse(
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
    IReadOnlyList<DecisionHistoryOptionItemResponse> Options,
    string Outcome,
    DateTime CreatedAtUtc);

public sealed record PagedDecisionHistoryResponse(
    IReadOnlyList<DecisionHistoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
