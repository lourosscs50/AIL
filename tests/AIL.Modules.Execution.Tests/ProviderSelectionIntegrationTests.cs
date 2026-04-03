using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.PolicyRegistry.Domain;
using AIL.Modules.ProviderRegistry.Application;
using AIL.Modules.ProviderRegistry.Domain;

namespace AIL.Modules.Execution.Tests;

public sealed class ProviderSelectionIntegrationTests
{
    [Fact]
    public async Task SelectAsync_Returns_Valid_Primary_And_Fallback_Targets()
    {
        var policyRegistry = new FakePolicyRegistryService(
            new ExecutionPolicy(
                PolicyKey: "policy.default",
                Description: "Default execution policy",
                PrimaryProviderKey: "stub-provider",
                PrimaryModelKey: "stub-model",
                FallbackAllowed: true,
                FallbackProviderKey: "openai",
                FallbackModelKey: "gpt-4",
                MaxTokens: 1024,
                TimeoutMs: 30000));

        var providerRegistry = new FakeProviderRegistryService(new[]
        {
            new ProviderDefinition(
                ProviderKey: "stub-provider",
                DefaultModelKey: "stub-model",
                SupportedModelKeys: new[] { "stub-model" },
                IsEnabled: true),
            new ProviderDefinition(
                ProviderKey: "openai",
                DefaultModelKey: "gpt-4",
                SupportedModelKeys: new[] { "gpt-4", "gpt-4.1-mini" },
                IsEnabled: true)
        });

        var service = new ProviderSelectionService(policyRegistry, providerRegistry);

        var result = await service.SelectAsync("portfolio.summary", CancellationToken.None);

        Assert.Equal("stub-provider", result.PrimaryProviderKey);
        Assert.Equal("stub-model", result.PrimaryModelKey);
        Assert.Equal("openai", result.FallbackProviderKey);
        Assert.Equal("gpt-4", result.FallbackModelKey);
        Assert.True(result.FallbackAllowed);
        Assert.Equal(1024, result.MaxTokens);
        Assert.Equal(30000, result.TimeoutMs);
    }

    [Fact]
    public async Task SelectAsync_Throws_When_Primary_Provider_Is_Not_Enabled()
    {
        var policyRegistry = new FakePolicyRegistryService(
            new ExecutionPolicy(
                PolicyKey: "policy.default",
                Description: "Default execution policy",
                PrimaryProviderKey: "openai",
                PrimaryModelKey: "gpt-4",
                FallbackAllowed: false,
                FallbackProviderKey: null,
                FallbackModelKey: null,
                MaxTokens: 1024,
                TimeoutMs: 30000));

        var providerRegistry = new FakeProviderRegistryService(new[]
        {
            new ProviderDefinition(
                ProviderKey: "openai",
                DefaultModelKey: "gpt-4",
                SupportedModelKeys: new[] { "gpt-4" },
                IsEnabled: false)
        });

        var service = new ProviderSelectionService(policyRegistry, providerRegistry);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SelectAsync("portfolio.summary", CancellationToken.None));

        Assert.Contains("primary provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectAsync_Throws_When_Primary_Model_Is_Not_Supported()
    {
        var policyRegistry = new FakePolicyRegistryService(
            new ExecutionPolicy(
                PolicyKey: "policy.default",
                Description: "Default execution policy",
                PrimaryProviderKey: "openai",
                PrimaryModelKey: "gpt-4o",
                FallbackAllowed: false,
                FallbackProviderKey: null,
                FallbackModelKey: null,
                MaxTokens: 1024,
                TimeoutMs: 30000));

        var providerRegistry = new FakeProviderRegistryService(new[]
        {
            new ProviderDefinition(
                ProviderKey: "openai",
                DefaultModelKey: "gpt-4",
                SupportedModelKeys: new[] { "gpt-4", "gpt-4.1-mini" },
                IsEnabled: true)
        });

        var service = new ProviderSelectionService(policyRegistry, providerRegistry);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SelectAsync("portfolio.summary", CancellationToken.None));

        Assert.Contains("model", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectAsync_Throws_When_Fallback_Model_Is_Not_Supported()
    {
        var policyRegistry = new FakePolicyRegistryService(
            new ExecutionPolicy(
                PolicyKey: "policy.default",
                Description: "Default execution policy",
                PrimaryProviderKey: "stub-provider",
                PrimaryModelKey: "stub-model",
                FallbackAllowed: true,
                FallbackProviderKey: "openai",
                FallbackModelKey: "bad-model",
                MaxTokens: 1024,
                TimeoutMs: 30000));

        var providerRegistry = new FakeProviderRegistryService(new[]
        {
            new ProviderDefinition(
                ProviderKey: "stub-provider",
                DefaultModelKey: "stub-model",
                SupportedModelKeys: new[] { "stub-model" },
                IsEnabled: true),
            new ProviderDefinition(
                ProviderKey: "openai",
                DefaultModelKey: "gpt-4",
                SupportedModelKeys: new[] { "gpt-4", "gpt-4.1-mini" },
                IsEnabled: true)
        });

        var service = new ProviderSelectionService(policyRegistry, providerRegistry);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SelectAsync("portfolio.summary", CancellationToken.None));

        Assert.Contains("fallback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakePolicyRegistryService : IPolicyRegistryService
    {
        private readonly ExecutionPolicy _policy;

        public FakePolicyRegistryService(ExecutionPolicy policy)
        {
            _policy = policy;
        }

        public Task<ExecutionPolicy> ResolvePolicyAsync(
            string capabilityKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_policy);
        }
    }

    private sealed class FakeProviderRegistryService : IProviderRegistryService
    {
        private readonly IReadOnlyList<ProviderDefinition> _providers;

        public FakeProviderRegistryService(IReadOnlyList<ProviderDefinition> providers)
        {
            _providers = providers;
        }

        public Task<IReadOnlyList<ProviderDefinition>> GetAllProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_providers);
        }

        public Task<ProviderDefinition?> ResolveProviderAsync(
            string providerKey,
            CancellationToken cancellationToken = default)
        {
            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(provider);
        }
    }
}