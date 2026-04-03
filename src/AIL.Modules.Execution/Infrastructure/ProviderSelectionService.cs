using AIL.Modules.ProviderRegistry.Application;
using AIL.Modules.ProviderRegistry.Domain;
using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.Execution.Application;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class ProviderSelectionService : IProviderSelectionService
{
    private readonly IPolicyRegistryService _policyRegistry;
    private readonly IProviderRegistryService _providerRegistry;

    public ProviderSelectionService(
        IPolicyRegistryService policyRegistry,
        IProviderRegistryService providerRegistry)
    {
        _policyRegistry = policyRegistry ?? throw new ArgumentNullException(nameof(policyRegistry));
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
    }

    public async Task<ProviderSelectionResult> SelectAsync(string capabilityKey, CancellationToken cancellationToken = default)
    {
        var policy = await _policyRegistry.ResolvePolicyAsync(capabilityKey, cancellationToken);

        // Validate the primary provider/model.
        var primary = await _providerRegistry.ResolveProviderAsync(policy.PrimaryProviderKey, cancellationToken);
        if (primary is null || !primary.IsEnabled)
        {
            throw new InvalidOperationException($"Primary provider '{policy.PrimaryProviderKey}' is not available.");
        }

        if (!string.IsNullOrWhiteSpace(policy.PrimaryModelKey)
            && !Array.Exists(primary.SupportedModelKeys, m => string.Equals(m, policy.PrimaryModelKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Model '{policy.PrimaryModelKey}' is not supported for provider '{policy.PrimaryProviderKey}'.");
        }

        string primaryModel = string.IsNullOrWhiteSpace(policy.PrimaryModelKey)
            ? primary.DefaultModelKey
            : policy.PrimaryModelKey;

        string? fallbackProvider = null;
        string? fallbackModel = null;

        if (policy.FallbackAllowed && !string.IsNullOrWhiteSpace(policy.FallbackProviderKey))
        {
            var fallback = await _providerRegistry.ResolveProviderAsync(policy.FallbackProviderKey, cancellationToken);
            if (fallback is null || !fallback.IsEnabled)
            {
                throw new InvalidOperationException($"Fallback provider '{policy.FallbackProviderKey}' is not available.");
            }

            fallbackProvider = fallback.ProviderKey;

            if (!string.IsNullOrWhiteSpace(policy.FallbackModelKey))
            {
                if (!Array.Exists(fallback.SupportedModelKeys, m => string.Equals(m, policy.FallbackModelKey, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Fallback model '{policy.FallbackModelKey}' is not supported for provider '{policy.FallbackProviderKey}'.");
                }

                fallbackModel = policy.FallbackModelKey;
            }
            else
            {
                fallbackModel = fallback.DefaultModelKey;
            }
        }

        return new ProviderSelectionResult(
            PrimaryProviderKey: primary.ProviderKey,
            PrimaryModelKey: primaryModel,
            FallbackProviderKey: fallbackProvider,
            FallbackModelKey: fallbackModel,
            FallbackAllowed: policy.FallbackAllowed,
            MaxTokens: policy.MaxTokens,
            TimeoutMs: policy.TimeoutMs);
    }
}
