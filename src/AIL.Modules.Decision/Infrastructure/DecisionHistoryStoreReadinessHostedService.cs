using System;
using System.Threading;
using System.Threading.Tasks;
using AIL.Modules.Decision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Eagerly initializes the durable decision history SQLite store during host startup. If the database cannot be created or opened,
/// <see cref="IHostedService.StartAsync"/> fails and the host does not complete startup (no silent durability). This runs before the server accepts requests.
/// </summary>
internal sealed class DecisionHistoryStoreReadinessHostedService : IHostedService
{
    private readonly IDbContextFactory<DecisionHistoryDbContext> _factory;

    public DecisionHistoryStoreReadinessHostedService(IDbContextFactory<DecisionHistoryDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        DecisionHistoryDatabaseInitializer.EnsureReady(_factory);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
