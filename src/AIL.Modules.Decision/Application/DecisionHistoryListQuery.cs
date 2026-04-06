using System;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Bounded filters for listing decision history. <see cref="TenantId"/> is required for tenant isolation.
/// Optional <c>CorrelationGroupId</c> / <c>MemoryInfluenceSummary</c> narrow results with exact matches (tenant-scoped first).
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
    string? MemoryInfluenceSummary = null);
