using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Domain;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.Observability.Application;

namespace AIL.Modules.Execution.Tests;

public sealed class ReliabilityFallbackTargetTests
{
    private static ExecutionReliabilityService CreateService(
        IProviderExecutionGatewayProvider resolver,
        IExecutionTelemetryService telemetry)
    {
        return new ExecutionReliabilityService(resolver, telemetry, new FakeAuditService());
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_Uses_Configured_Fallback_Target()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new FailingGateway("stub-provider"),
            new SuccessGateway("openai", "gpt-4"));

        var service = CreateService(resolver, telemetry);

        var request = new ProviderExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "portfolio.summary",
            PromptKey: "system.summary",
            PromptVersion: "v1",
            PromptText: "Summarize the portfolio.",
            ContextText: "context=[project=AIL]",
            MaxTokens: 512,
            FallbackAllowed: true,
            PrimaryProviderKey: "stub-provider",
            PrimaryModelKey: "stub-model",
            FallbackProviderKey: "openai",
            FallbackModelKey: "gpt-4",
            Metadata: new Dictionary<string, string>());

        var result = await service.ExecuteWithReliabilityAsync(request, CancellationToken.None);

        Assert.Equal("openai", result.ProviderKey);
        Assert.Equal("gpt-4", result.ModelKey);
        Assert.True(result.UsedFallback);
        Assert.Contains("openai", result.OutputText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_Does_Not_Fallback_When_Disallowed()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new FailingGateway("stub-provider"),
            new SuccessGateway("openai", "gpt-4"));

        var service = CreateService(resolver, telemetry);

        var request = new ProviderExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "portfolio.summary",
            PromptKey: "system.summary",
            PromptVersion: "v1",
            PromptText: "Summarize the portfolio.",
            ContextText: "context=[project=AIL]",
            MaxTokens: 512,
            FallbackAllowed: false,
            PrimaryProviderKey: "stub-provider",
            PrimaryModelKey: "stub-model",
            FallbackProviderKey: "openai",
            FallbackModelKey: "gpt-4",
            Metadata: new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(request, CancellationToken.None));

        Assert.True(
            ex.FailureType == ExecutionFailureType.TransientFailure ||
            ex.FailureType == ExecutionFailureType.Timeout);
    }

    private sealed class FailingGateway : IProviderExecutionGateway
    {
        public FailingGateway(string providerKey)
        {
            ProviderKey = providerKey;
        }

        public string ProviderKey { get; }

        public Task<ProviderExecutionResult> ExecuteAsync(
            ProviderExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new ExecutionReliabilityException(
                $"Simulated failure for {ProviderKey}.",
                ExecutionFailureType.TransientFailure);
        }
    }

    private sealed class SuccessGateway : IProviderExecutionGateway
    {
        private readonly string _modelKey;

        public SuccessGateway(string providerKey, string modelKey)
        {
            ProviderKey = providerKey;
            _modelKey = modelKey;
        }

        public string ProviderKey { get; }

        public Task<ProviderExecutionResult> ExecuteAsync(
            ProviderExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderExecutionResult(
                ProviderKey: ProviderKey,
                ModelKey: _modelKey,
                OutputText: $"success from {ProviderKey}",
                UsedFallback: true,
                InputTokenCount: 25,
                OutputTokenCount: 12));
        }
    }

    private sealed class FakeProviderExecutionGatewayProvider : IProviderExecutionGatewayProvider
    {
        private readonly Dictionary<string, IProviderExecutionGateway> _gateways;

        public FakeProviderExecutionGatewayProvider(params IProviderExecutionGateway[] gateways)
        {
            _gateways = gateways.ToDictionary(g => g.ProviderKey, StringComparer.OrdinalIgnoreCase);
        }

        public IProviderExecutionGateway Resolve(string providerKey)
        {
            if (_gateways.TryGetValue(providerKey, out var gateway))
            {
                return gateway;
            }

            throw new InvalidOperationException($"No gateway registered for provider '{providerKey}'.");
        }
    }

    private sealed class FakeExecutionTelemetryService : IExecutionTelemetryService
    {
        public List<ExecutionTelemetry> Items { get; } = new();

        public Task TrackAsync(ExecutionTelemetry telemetry, CancellationToken cancellationToken = default)
        {
            Items.Add(telemetry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task<Guid> RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Guid.NewGuid());
        }
    }
}