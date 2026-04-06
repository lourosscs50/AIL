using System;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Bounded filters for listing decision history. <see cref="TenantId"/> is required for tenant isolation.
/// </summary>
public sealed record DecisionHistoryListQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? DecisionType = null,
    string? SelectedStrategyKey = null,
    string? PolicyKey = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null);
