using AIL.Modules.Execution.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AIL.Modules.Execution.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExecutionModule(this IServiceCollection services)
    {
        // Default to the stub gateway for environments where configuration isn't provided.
        services.AddSingleton<IProviderExecutionGateway, StubProviderExecutionGateway>();
        services.AddSingleton<IProviderExecutionGatewayProvider, ProviderExecutionGatewayResolver>();
        services.AddSingleton<IProviderSelectionService, ProviderSelectionService>();
        services.AddSingleton<IExecutionReliabilityService, ExecutionReliabilityService>();
        services.AddSingleton<IExecutionService, ExecutionService>();
        return services;
    }

    public static IServiceCollection AddExecutionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var providerOptions = configuration.GetSection(ProviderOptions.SectionName).Get<ProviderOptions>() ?? new ProviderOptions();

        if (string.Equals(providerOptions.Mode, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

            var openAiOptions = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>() ?? new OpenAiOptions();
            if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
            {
                throw new InvalidOperationException("OpenAI provider is enabled, but Providers:OpenAI:ApiKey is not configured.");
            }

            services.AddHttpClient<OpenAiProviderExecutionGateway>(client =>
            {
                // BaseUrl is applied in the gateway build request; but setting BaseAddress here can help in case of relative paths.
                client.BaseAddress = new Uri(openAiOptions.BaseUrl);
            });

            services.AddSingleton<IProviderExecutionGateway, OpenAiProviderExecutionGateway>();
        }
        else
        {
            services.AddSingleton<IProviderExecutionGateway, StubProviderExecutionGateway>();
        }

        services.AddSingleton<IProviderExecutionGatewayProvider, ProviderExecutionGatewayResolver>();
        services.AddSingleton<IProviderSelectionService, ProviderSelectionService>();
        services.AddSingleton<IExecutionReliabilityService, ExecutionReliabilityService>();
        services.AddSingleton<IExecutionService, ExecutionService>();
        return services;
    }
}
