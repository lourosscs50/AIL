using System;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure.Persistence;
using AIL.Modules.Decision.Infrastructure.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AIL.Modules.Decision.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers SQLite-backed <see cref="IDecisionHistoryStore"/> (durable). Connection strings are validated immediately; invalid configuration throws <see cref="InvalidOperationException"/> during registration.
    /// When running inside a generic host (for example the AIL API), <see cref="DecisionHistoryStoreReadinessHostedService"/> runs at startup and applies pending EF Core migrations for <see cref="Persistence.DecisionHistoryDbContext"/> before the server accepts traffic.
    /// Consumers that only build an <see cref="IServiceProvider"/> without running hosted services still get deterministic initialization on the first store operation via <see cref="EfDecisionHistoryStore"/>.
    /// Pass <paramref name="hostEnvironment"/> from the host so non-development environments require an explicit <c>DecisionHistory:SqliteConnectionString</c> in configuration (see <see cref="DecisionHistoryPersistenceOptions"/>).
    /// </summary>
    public static IServiceCollection AddDecisionHistoryStore(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        IHostEnvironment? hostEnvironment = null)
    {
        if (configuration is null && hostEnvironment is not null && !hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Decision history durable store registration requires IConfiguration when the host environment is not Development. " +
                "Supply configuration (for example appsettings or environment variables) and set DecisionHistory:SqliteConnectionString.");
        }

        DecisionHistoryPersistenceOptions opts;
        string raw;

        if (configuration is null)
        {
            opts = new DecisionHistoryPersistenceOptions();
            raw = opts.SqliteConnectionString;
        }
        else
        {
            opts = configuration.GetSection(DecisionHistoryPersistenceOptions.SectionName).Get<DecisionHistoryPersistenceOptions>()
                   ?? new DecisionHistoryPersistenceOptions();

            if (hostEnvironment is not null && !hostEnvironment.IsDevelopment())
            {
                var explicitFromConfig =
                    configuration.GetSection(DecisionHistoryPersistenceOptions.SectionName)["SqliteConnectionString"];
                if (string.IsNullOrWhiteSpace(explicitFromConfig))
                {
                    throw new InvalidOperationException(
                        "DecisionHistory:SqliteConnectionString must be explicitly set in configuration when the host environment is not Development. " +
                        "Configure a non-empty SQLite connection string (for example in appsettings or environment variables such as DecisionHistory__SqliteConnectionString).");
                }

                raw = explicitFromConfig.Trim();
            }
            else
            {
                raw = string.IsNullOrWhiteSpace(opts.SqliteConnectionString)
                    ? DecisionHistoryPersistenceOptions.DevelopmentDefaultSqliteConnectionString
                    : opts.SqliteConnectionString.Trim();
            }
        }

        var conn = DecisionHistoryPersistenceValidator.ValidateAndNormalizeSqliteConnectionString(raw);

        services.AddDbContextFactory<DecisionHistoryDbContext>(options =>
            options.UseSqlite(conn));

        services.TryAddSingleton<IDecisionHistoryStore, EfDecisionHistoryStore>();
        services.AddHostedService<DecisionHistoryStoreReadinessHostedService>();
        return services;
    }

    public static IServiceCollection AddDecisionModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, CandidateMatchDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, ContextEscalatedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, MemoryInformedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DefaultSafeDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DecisionContinuityStrategy>());

        services.TryAddSingleton<IDecisionPolicyService, DefaultDecisionPolicyService>();
        services.AddSingleton<DecisionService>();
        services.AddSingleton<IDecisionService>(sp => sp.GetRequiredService<DecisionService>());
        services.AddDecisionHistoryStore(configuration, hostEnvironment);
        services.TryAddSingleton<IDecisionHistoryRecorder, DecisionHistoryRecorder>();
        return services;
    }
}
