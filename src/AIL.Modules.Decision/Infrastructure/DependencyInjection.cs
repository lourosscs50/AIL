using System;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure.Persistence;
using AIL.Modules.Decision.Infrastructure.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIL.Modules.Decision.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers SQLite-backed <see cref="IDecisionHistoryStore"/> (durable). Use <see cref="DecisionHistoryPersistenceOptions"/> under <c>DecisionHistory</c> configuration.
    /// </summary>
    public static IServiceCollection AddDecisionHistoryStore(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var opts = configuration is null
            ? new DecisionHistoryPersistenceOptions()
            : configuration.GetSection(DecisionHistoryPersistenceOptions.SectionName).Get<DecisionHistoryPersistenceOptions>()
              ?? new DecisionHistoryPersistenceOptions();
        var conn = string.IsNullOrWhiteSpace(opts.SqliteConnectionString)
            ? new DecisionHistoryPersistenceOptions().SqliteConnectionString
            : opts.SqliteConnectionString.Trim();

        services.AddDbContextFactory<DecisionHistoryDbContext>(options =>
            options.UseSqlite(conn));

        services.TryAddSingleton<IDecisionHistoryStore, EfDecisionHistoryStore>();
        return services;
    }

    public static IServiceCollection AddDecisionModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, CandidateMatchDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, ContextEscalatedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, MemoryInformedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DefaultSafeDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DecisionContinuityStrategy>());

        services.TryAddSingleton<IDecisionPolicyService, DefaultDecisionPolicyService>();
        services.AddSingleton<DecisionService>();
        services.AddSingleton<IDecisionService>(sp => sp.GetRequiredService<DecisionService>());
        services.AddDecisionHistoryStore(configuration);
        services.TryAddSingleton<IDecisionHistoryRecorder, DecisionHistoryRecorder>();
        return services;
    }
}
