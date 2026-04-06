namespace AIL.Modules.Decision.Application;

/// <summary>
/// Bounded sort key for decision history list queries. Extend only when the store can apply ordering deterministically.
/// </summary>
public enum DecisionHistorySortBy
{
    CreatedAtUtc = 0
}

/// <summary>
/// Sort direction for <see cref="DecisionHistorySortBy"/>. Tie-breaker is always <c>Id</c> ascending for stable paging.
/// </summary>
public enum DecisionHistorySortDirection
{
    Ascending = 0,
    Descending = 1
}
