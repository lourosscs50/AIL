namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Connection settings for SQLite decision history persistence (bounded to connection string only).
/// Values are validated at registration time; invalid SQLite connection strings prevent service registration from succeeding.
/// </summary>
public sealed class DecisionHistoryPersistenceOptions
{
    public const string SectionName = "DecisionHistory";

    /// <summary>
    /// SQLite connection string (for example <c>Data Source=ail_decision_history.db</c>). Must be non-empty and parse as a valid <c>Microsoft.Data.Sqlite</c> connection string with a resolvable data source.
    /// </summary>
    public string SqliteConnectionString { get; set; } = "Data Source=ail_decision_history.db";
}
