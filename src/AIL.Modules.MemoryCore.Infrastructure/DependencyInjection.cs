using AIL.Modules.MemoryCore.Application;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace AIL.Modules.MemoryCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMemoryCoreModule(this IServiceCollection services, bool useInMemory = true, string? persistenceFilePath = null)
    {
        services.AddSingleton<IMemoryRepository>(sp =>
        {
            if (useInMemory)
                return new InMemoryMemoryRepository();

            var path = string.IsNullOrWhiteSpace(persistenceFilePath)
                ? Path.Combine(AppContext.BaseDirectory, "memory-core-store.json")
                : persistenceFilePath;

            return new FileMemoryRepository(path);
        });

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IMemoryService, MemoryService>();

        return services;
    }
}
