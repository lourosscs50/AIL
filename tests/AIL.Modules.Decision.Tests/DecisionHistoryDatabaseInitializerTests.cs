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
/// Schema lifecycle: <see cref="DecisionHistoryDatabaseInitializer"/> must apply migrations idempotently for fresh and existing SQLite files.
/// </summary>
public sealed class DecisionHistoryDatabaseInitializerTests
{
    [Fact]
    public void EnsureReady_Baselines_LegacyEnsureCreatedDatabase_ThenMigrateSucceeds()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_legacy_{Guid.NewGuid():N}.db");
        try
        {
            var legacyOpts = new DbContextOptionsBuilder<DecisionHistoryDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            using (var legacy = new DecisionHistoryDbContext(legacyOpts))
            {
                legacy.Database.EnsureCreated();
            }

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

            using (var db = factory.CreateDbContext())
            {
                Assert.Empty(db.Database.GetPendingMigrations());
            }
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete(dbPath + "-shm");
            TryDelete(dbPath + "-wal");
        }
    }

    [Fact]
    public void EnsureReady_AppliesMigrations_Idempotently_OnSameDatabaseFile()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_migrate_{Guid.NewGuid():N}.db");
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
            DecisionHistoryDatabaseInitializer.EnsureReady(factory);

            using (var db = factory.CreateDbContext())
            {
                Assert.Empty(db.Database.GetPendingMigrations());
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
