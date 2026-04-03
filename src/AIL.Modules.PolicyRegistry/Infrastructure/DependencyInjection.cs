using AIL.Modules.PolicyRegistry.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.PolicyRegistry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPolicyRegistryModule(this IServiceCollection services)
    {
        services.AddSingleton<IPolicyRegistryService, PolicyRegistryService>();
        return services;
    }
}
