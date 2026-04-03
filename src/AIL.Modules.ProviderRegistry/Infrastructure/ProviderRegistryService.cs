using AIL.Modules.ProviderRegistry.Application;
using AIL.Modules.ProviderRegistry.Domain;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.ProviderRegistry.Infrastructure;

internal sealed class ProviderRegistryService : IProviderRegistryService
{
    private readonly IReadOnlyList<ProviderDefinition> _providers;

    public ProviderRegistryService(IConfiguration configuration)
    {
        // Resolve provider activation and defaults from configuration.
        // This is intentionally simple for Phase 9: providers are enabled via Providers:Mode and OpenAI config.

        var providers = new List<ProviderDefinition>
        {
            new ProviderDefinition(
                ProviderKey: "stub-provider",
                DefaultModelKey: "stub-model",
                SupportedModelKeys: new[] { "stub-model" },
                IsEnabled: true),
        };

        var mode = configuration.GetValue<string>("Providers:Mode");
        if (string.Equals(mode, "OpenAI", System.StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = configuration.GetValue<string>("Providers:OpenAI:ApiKey");
            var model = configuration.GetValue<string>("Providers:OpenAI:Model");

            var isEnabled = !string.IsNullOrWhiteSpace(apiKey);
            providers.Add(new ProviderDefinition(
                ProviderKey: "openai",
                DefaultModelKey: string.IsNullOrWhiteSpace(model) ? "gpt-4" : model,
                SupportedModelKeys: new[] { model ?? "gpt-4" },
                IsEnabled: isEnabled));
        }

        _providers = providers;
    }

    public Task<IReadOnlyList<ProviderDefinition>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_providers);

    public Task<ProviderDefinition?> ResolveProviderAsync(string providerKey, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.ProviderKey, providerKey, System.StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(provider);
    }
}
