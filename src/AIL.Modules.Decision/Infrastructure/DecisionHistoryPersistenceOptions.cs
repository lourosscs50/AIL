namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Connection settings for SQLite decision history persistence (bounded to connection string only).
/// Values are validated at registration time; invalid SQLite connection strings prevent service registration from succeeding.
/// This options type only describes infrastructure configuration and is not part of public API contracts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Development</b>: When <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/> is <c>Development</c>,
/// a missing <c>DecisionHistory</c> configuration section may use the default file path below for local runs.
/// </para>
/// <para>
/// <b>Non-development</b>: <c>DecisionHistory:SqliteConnectionString</c> must be present and non-empty in configuration
/// (for example <c>appsettings.Production.json</c> or environment variables such as <c>DecisionHistory__SqliteConnectionString</c>).
/// Relying on an implicit default file path without an explicit setting is not allowed—registration fails with an honest error.
/// </para>
/// </remarks>
public sealed class DecisionHistoryPersistenceOptions
{
    public const string SectionName = "DecisionHistory";

    /// <summary>
    /// Default SQLite connection string used only when the host environment is <c>Development</c> and no explicit value is configured.
    /// </summary>
    public const string DevelopmentDefaultSqliteConnectionString = "Data Source=ail_decision_history.db";

    /// <summary>
    /// SQLite connection string (for example <c>Data Source=ail_decision_history.db</c>). Must be non-empty and parse as a valid <c>Microsoft.Data.Sqlite</c> connection string with a resolvable data source.
    /// </summary>
    public string SqliteConnectionString { get; set; } = DevelopmentDefaultSqliteConnectionString;
}
