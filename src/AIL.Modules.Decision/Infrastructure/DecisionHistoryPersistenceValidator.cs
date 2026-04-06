using System;
using Microsoft.Data.Sqlite;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Narrow validation for <see cref="DecisionHistoryPersistenceOptions.SqliteConnectionString"/> so bad configuration fails at registration time.
/// </summary>
internal static class DecisionHistoryPersistenceValidator
{
    /// <summary>
    /// Parses and validates a SQLite connection string. Throws <see cref="InvalidOperationException"/> when the value cannot be used for durable decision history.
    /// </summary>
    public static string ValidateAndNormalizeSqliteConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "DecisionHistory:SqliteConnectionString is required. Provide a non-empty SQLite connection string (for example Data Source=ail_decision_history.db).");
        }

        var trimmed = raw.Trim();
        try
        {
            var builder = new SqliteConnectionStringBuilder(trimmed);
            if (string.IsNullOrEmpty(builder.DataSource))
            {
                throw new InvalidOperationException(
                    "DecisionHistory:SqliteConnectionString must specify a SQLite data source (for example Data Source=path.db, Data Source=:memory:, or Mode=Memory).");
            }

            return trimmed;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "DecisionHistory:SqliteConnectionString is not a valid SQLite connection string.", ex);
        }
    }
}
