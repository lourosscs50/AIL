using AIL.Modules.ProviderRegistry.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.ProviderRegistry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProviderRegistryModule(this IServiceCollection services)
    {
        services.AddSingleton<IProviderRegistryService, ProviderRegistryService>();
        return services;
    }
}
