using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIL.Modules.Decision.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDecisionModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, CandidateMatchDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, ContextEscalatedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, MemoryInformedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DefaultSafeDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DecisionContinuityStrategy>());

        services.TryAddSingleton<IDecisionPolicyService, DefaultDecisionPolicyService>();
        services.AddSingleton<DecisionService>();
        services.AddSingleton<IDecisionService>(sp => sp.GetRequiredService<DecisionService>());
        services.TryAddSingleton<IDecisionHistoryStore, InMemoryDecisionHistoryStore>();
        services.TryAddSingleton<IDecisionHistoryRecorder, DecisionHistoryRecorder>();
        return services;
    }
}
