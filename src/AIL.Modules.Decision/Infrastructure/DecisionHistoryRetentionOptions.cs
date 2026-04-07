namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Bounded retention parameters for <see cref="IDecisionHistoryStore"/> implementations (e.g. the default in-memory store).
/// Only a maximum row count is enforced; there is no time-based expiry in this in-memory implementation.
/// When the limit is exceeded on a <b>new</b> insert, the oldest-inserted row is removed first (FIFO by insert order).
/// Totals and list queries reflect only rows that remain in the store after eviction.
/// </summary>
internal sealed class DecisionHistoryRetentionOptions
{
    /// <summary>Default cap aligned with the previous fixed store limit.</summary>
    public const int DefaultMaxRetainedRecords = 512;

    /// <summary>
    /// Maximum number of distinct decision history records kept in memory across all tenants.
    /// Must be at least 1.
    /// </summary>
    public int MaxRetainedRecords { get; init; } = DefaultMaxRetainedRecords;
}
