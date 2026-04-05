using AIL.Modules.Observability.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.Observability.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddObservabilityModule(this IServiceCollection services)
    {
        services.AddSingleton<IExecutionTelemetryService, ExecutionTelemetryService>();
        services.AddSingleton<IDecisionTelemetryService, DecisionTelemetryService>();
        return services;
    }
}
