namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Connection settings for SQLite decision history persistence (bounded to connection string only).
/// </summary>
public sealed class DecisionHistoryPersistenceOptions
{
    public const string SectionName = "DecisionHistory";

    /// <summary>SQLite connection string (e.g. <c>Data Source=ail_decision_history.db</c>).</summary>
    public string SqliteConnectionString { get; set; } = "Data Source=ail_decision_history.db";
}
