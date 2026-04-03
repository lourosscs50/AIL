using AIL.Modules.Audit.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AIL.Modules.Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddSingleton<IAuditService, AuditService>();
        return services;
    }
}
