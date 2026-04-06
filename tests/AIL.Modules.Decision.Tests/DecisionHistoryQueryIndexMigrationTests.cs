using System;
using System.Collections.Generic;
using System.IO;
using AIL.Modules.Decision.Infrastructure;
using AIL.Modules.Decision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIL.Modules.Decision.Tests;

/// <summary>
/// Verifies compiled migrations produce the hardened index set (no API/contract surface).
/// </summary>
public sealed class DecisionHistoryQueryIndexMigrationTests
{
    [Fact]
    public void Migrate_CreatesExpectedDecisionHistoryIndexes_OnFreshDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_ix_{Guid.NewGuid():N}.db");
        try
        {
            var config = new ConfigurationBuilder()
                .Add(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string?>
                    {
                        ["DecisionHistory:SqliteConnectionString"] = $"Data Source={dbPath}",
                    },
                })
                .Build();
            var services = new ServiceCollection();
            services.AddDecisionHistoryStore(config);
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IDbContextFactory<DecisionHistoryDbContext>>();

            DecisionHistoryDatabaseInitializer.EnsureReady(factory);

            using var db = factory.CreateDbContext();
            var conn = db.Database.GetDbConnection();
            conn.Open();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT name FROM sqlite_master
                    WHERE type = 'index' AND tbl_name = 'DecisionHistory' AND name NOT LIKE 'sqlite_%'
                    ORDER BY name;
                    """;
                using var reader = cmd.ExecuteReader();
                var names = new List<string>();
                while (reader.Read())
                    names.Add(reader.GetString(0));

                Assert.Contains("IX_DecisionHistory_TenantId_CreatedAtUtc", names);
                Assert.Contains("IX_DecisionHistory_TenantId_DecisionType_CreatedAtUtc", names);
                Assert.Contains("IX_DecisionHistory_TenantId_CorrelationGroupId_CreatedAtUtc", names);
                Assert.Contains("IX_DecisionHistory_TenantId_ExecutionInstanceId_CreatedAtUtc", names);
                Assert.DoesNotContain("IX_DecisionHistory_TenantId", names);
            }
            finally
            {
                conn.Close();
            }
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete(dbPath + "-shm");
            TryDelete(dbPath + "-wal");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }
}
