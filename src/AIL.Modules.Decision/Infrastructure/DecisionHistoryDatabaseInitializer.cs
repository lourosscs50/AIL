using System;
using System.Data;
using System.Data.Common;
using AIL.Modules.Decision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Shared SQLite schema lifecycle for durable decision history (<see cref="EfDecisionHistoryStore"/>).
/// </summary>
internal static class DecisionHistoryDatabaseInitializer
{
    /// <summary>
    /// Must stay aligned with the initial EF Core migration id under <c>Persistence/Migrations/</c>.
    /// </summary>
    internal const string InitialMigrationId = "20260406223254_InitialDecisionHistory";

    private const string EfProductVersion = "10.0.5";

    /// <summary>
    /// Applies pending EF Core migrations (<see cref="RelationalDatabaseFacadeExtensions.Migrate(DatabaseFacade)"/>). Idempotent when the database is already up to date.
    /// If the SQLite file was created with legacy <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated"/> (decision history table present, no migration history),
    /// records the initial migration without re-running DDL so migration application can succeed honestly.
    /// Invoked from <see cref="DecisionHistoryStoreReadinessHostedService"/> at API host startup, and from <see cref="EfDecisionHistoryStore"/> when hosted services do not run.
    /// </summary>
    public static void EnsureReady(IDbContextFactory<DecisionHistoryDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var db = factory.CreateDbContext();
        TryBaselineLegacyEnsureCreatedDatabase(db);
        db.Database.Migrate();
    }

    private static void TryBaselineLegacyEnsureCreatedDatabase(DecisionHistoryDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            connection.Open();
        try
        {
            if (!SqliteTableExists(connection, "DecisionHistory"))
                return;

            if (SqliteTableExists(connection, "__EFMigrationsHistory"))
            {
                if (MigrationRowExists(connection))
                    return;
            }
            else
            {
                db.Database.ExecuteSqlRaw(
                    """
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """);
            }

            db.Database.ExecuteSqlRaw(
                """
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({0}, {1});
                """,
                InitialMigrationId,
                EfProductVersion);
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
                connection.Close();
        }
    }

    private static bool SqliteTableExists(DbConnection connection, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = name;
        cmd.Parameters.Add(p);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    private static bool MigrationRowExists(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = $id;""";
        var p = cmd.CreateParameter();
        p.ParameterName = "$id";
        p.Value = InitialMigrationId;
        cmd.Parameters.Add(p);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }
}
