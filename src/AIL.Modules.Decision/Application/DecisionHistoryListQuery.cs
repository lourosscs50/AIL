using System;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Bounded filters for listing decision history. <see cref="TenantId"/> is required for tenant isolation.
/// Filters combine conjunctively (AND). <see cref="CreatedFromUtc"/> / <see cref="CreatedToUtc"/> are inclusive range bounds on <c>CreatedAtUtc</c>.
/// String filters use ordinal case-sensitive equality unless noted otherwise at the API boundary.
/// Sorting is bounded to <see cref="SortBy"/> with stable tie-break on record <c>Id</c> ascending.
/// </summary>
public sealed record DecisionHistoryListQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? DecisionType = null,
    string? SelectedStrategyKey = null,
    string? PolicyKey = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    Guid? CorrelationGroupId = null,
    string? MemoryInfluenceSummary = null,
    Guid? ExecutionInstanceId = null,
    DecisionHistorySortBy SortBy = DecisionHistorySortBy.CreatedAtUtc,
    DecisionHistorySortDirection SortDirection = DecisionHistorySortDirection.Descending);
