using AIL.Modules.ContextEngine.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.ContextEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddContextEngineModule(this IServiceCollection services)
    {
        services.AddSingleton<IContextEngineService, ContextEngineService>();
        return services;
    }
}
