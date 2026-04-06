using System;
using AIL.Modules.Decision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Shared SQLite schema creation for durable decision history (<see cref="EfDecisionHistoryStore"/>).
/// </summary>
internal static class DecisionHistoryDatabaseInitializer
{
    /// <summary>
    /// Creates the SQLite database and schema if they do not exist (<see cref="DatabaseFacade.EnsureCreated"/>). Idempotent.
    /// Invoked from <see cref="DecisionHistoryStoreReadinessHostedService"/> at API host startup (eager readiness), and from
    /// <see cref="EfDecisionHistoryStore"/> on first use when no host pipeline runs (for example unit tests that build a service provider without starting hosted services).
    /// </summary>
    public static void EnsureReady(IDbContextFactory<DecisionHistoryDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }
}
