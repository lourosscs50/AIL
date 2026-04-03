using AIL.Modules.Security.Application;
using AIL.Modules.Security.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.Security.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        // NOTE: This is a minimal placeholder implementation to bootstrap the security module.
        //       Replace with real tenant/role validation and resource authorization in the future.
        services.AddSingleton<ISecurityService, SecurityService>();

        return services;
    }
}
