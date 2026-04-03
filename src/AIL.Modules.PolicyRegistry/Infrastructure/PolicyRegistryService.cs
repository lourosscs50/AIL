using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.PolicyRegistry.Domain;

namespace AIL.Modules.PolicyRegistry.Infrastructure;

internal sealed class PolicyRegistryService : IPolicyRegistryService
{
    public Task<ExecutionPolicy> ResolvePolicyAsync(string capabilityKey, CancellationToken cancellationToken = default)
    {
        // Placeholder policy resolution.
        // In a real implementation, this would pull from a policy store or configuration.
        var policy = new ExecutionPolicy(
            PolicyKey: capabilityKey,
            Description: "Default allow policy",
            PrimaryProviderKey: "stub-provider",
            PrimaryModelKey: "stub-model",
            FallbackAllowed: true,
            FallbackProviderKey: null,
            FallbackModelKey: null,
            MaxTokens: 1024,
            TimeoutMs: 30000);

        return Task.FromResult(policy);
    }
}
