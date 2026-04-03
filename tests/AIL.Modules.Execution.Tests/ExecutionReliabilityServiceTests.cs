using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Domain;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.Observability.Application;

namespace AIL.Modules.Execution.Tests;

/// <summary>
/// Phase 8 + 9 reliability tests aligned to the current contracts:
/// reliability uses gateway resolution, retries transient failures,
/// respects fallback settings, and emits telemetry.
/// </summary>
public sealed class ExecutionReliabilityServiceTests
{
    private static ProviderExecutionRequest CreateRequest(bool fallbackAllowed = true) =>
        new(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "test-capability",
            PromptKey: "test-prompt",
            PromptVersion: "1.0",
            PromptText: "What is AI?",
            ContextText: "refs=[] vars=[]",
            MaxTokens: 1024,
            FallbackAllowed: fallbackAllowed,
            PrimaryProviderKey: "openai",
            PrimaryModelKey: "gpt-4",
            FallbackProviderKey: "stub-provider",
            FallbackModelKey: "stub-model",
            Metadata: new Dictionary<string, string> { ["key"] = "value" });

    private static ExecutionReliabilityService CreateService(
        IProviderExecutionGatewayProvider resolver,
        IExecutionTelemetryService telemetry)
    {
        return new ExecutionReliabilityService(resolver, telemetry, new FakeAuditService());
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnSuccessfulExecution_ReturnsResult()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var audit = new FakeAuditService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ProviderExecutionResult(
                    ProviderKey: "openai",
                    ModelKey: "gpt-4",
                    OutputText: "AI is...",
                    UsedFallback: false,
                    InputTokenCount: 10,
                    OutputTokenCount: 20)));

        var service = new ExecutionReliabilityService(resolver, telemetry, audit);

        var result = await service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("openai", result.ProviderKey);
        Assert.Equal("gpt-4", result.ModelKey);
        Assert.Equal("AI is...", result.OutputText);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnTimeout_ThrowsReliabilityException()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout),
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout),
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout)),
            new SequenceGateway(
                "stub-provider",
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout)));

        var service = CreateService(resolver, telemetry);

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(ExecutionFailureType.Timeout, ex.FailureType);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnTimeout_EmitsFailureTelemetry()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout),
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout),
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout)),
            new SequenceGateway(
                "stub-provider",
                new ExecutionReliabilityException("Timeout", ExecutionFailureType.Timeout)));

        var service = CreateService(resolver, telemetry);

        await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None));

        Assert.Contains(telemetry.Items, x => !x.Succeeded);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnTransientFailure_RetriesAndSucceeds()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure),
                new ProviderExecutionResult(
                    ProviderKey: "openai",
                    ModelKey: "gpt-4",
                    OutputText: "Recovered on retry",
                    UsedFallback: false,
                    InputTokenCount: 10,
                    OutputTokenCount: 20)));

        var service = CreateService(resolver, telemetry);

        var result = await service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("Recovered on retry", result.OutputText);
        Assert.Equal("openai", result.ProviderKey);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnTransientFailure_EmitsTelemetry()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure),
                new ProviderExecutionResult(
                    ProviderKey: "openai",
                    ModelKey: "gpt-4",
                    OutputText: "Recovered on retry",
                    UsedFallback: false,
                    InputTokenCount: 10,
                    OutputTokenCount: 20)));

        var service = CreateService(resolver, telemetry);

        await service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None);

        Assert.NotEmpty(telemetry.Items);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnPrimaryFailureWithFallback_AttemptsFallback()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var resolver = new FakeProviderExecutionGatewayProvider(
            new SequenceGateway(
                "openai",
                new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure),
                new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure)),
            new SequenceGateway(
                "stub-provider",
                new ProviderExecutionResult(
                    ProviderKey: "stub-provider",
                    ModelKey: "stub-model",
                    OutputText: "Fallback response",
                    UsedFallback: true,
                    InputTokenCount: 5,
                    OutputTokenCount: 15)));

        var service = CreateService(resolver, telemetry);

        var result = await service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Equal("stub-provider", result.ProviderKey);
        Assert.Equal("Fallback response", result.OutputText);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnPrimaryFailureWithoutFallback_FailsWithoutFallbackAttempt()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var primaryGateway = new SequenceGateway(
            "openai",
            new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure),
            new ExecutionReliabilityException("503", ExecutionFailureType.TransientFailure));

        var fallbackGateway = new SequenceGateway(
            "stub-provider",
            new ProviderExecutionResult(
                ProviderKey: "stub-provider",
                ModelKey: "stub-model",
                OutputText: "Should not be used",
                UsedFallback: true,
                InputTokenCount: 1,
                OutputTokenCount: 1));

        var resolver = new FakeProviderExecutionGatewayProvider(primaryGateway, fallbackGateway);
        var service = CreateService(resolver, telemetry);

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(fallbackAllowed: false), CancellationToken.None));

        Assert.Equal(ExecutionFailureType.TransientFailure, ex.FailureType);
        Assert.Equal(0, fallbackGateway.CallCount);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnAccessDenied_DoesNotRetry()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var primaryGateway = new SequenceGateway(
            "openai",
            new ExecutionReliabilityException("Unauthorized", ExecutionFailureType.AccessDenied));

        var resolver = new FakeProviderExecutionGatewayProvider(primaryGateway);
        var service = CreateService(resolver, telemetry);

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(ExecutionFailureType.AccessDenied, ex.FailureType);
        Assert.Equal(1, primaryGateway.CallCount);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnBadRequest_DoesNotRetry()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var primaryGateway = new SequenceGateway(
            "openai",
            new ExecutionReliabilityException("Bad request", ExecutionFailureType.BadRequest));

        var resolver = new FakeProviderExecutionGatewayProvider(primaryGateway);
        var service = CreateService(resolver, telemetry);

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(ExecutionFailureType.BadRequest, ex.FailureType);
        Assert.Equal(1, primaryGateway.CallCount);
    }

    [Fact]
    public async Task ExecuteWithReliabilityAsync_OnNonTransientFailure_FailsWithoutRetry()
    {
        var telemetry = new FakeExecutionTelemetryService();
        var primaryGateway = new SequenceGateway(
            "openai",
            new ExecutionReliabilityException("Unexpected error", ExecutionFailureType.NonTransientFailure));

        var resolver = new FakeProviderExecutionGatewayProvider(primaryGateway);
        var service = CreateService(resolver, telemetry);

        var ex = await Assert.ThrowsAsync<ExecutionReliabilityException>(
            () => service.ExecuteWithReliabilityAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(ExecutionFailureType.NonTransientFailure, ex.FailureType);
        Assert.Equal(1, primaryGateway.CallCount);
    }

    private sealed class SequenceGateway : IProviderExecutionGateway
    {
        private readonly Queue<object> _steps;

        public SequenceGateway(string providerKey, params object[] steps)
        {
            ProviderKey = providerKey;
            _steps = new Queue<object>(steps);
        }

        public string ProviderKey { get; }

        public int CallCount { get; private set; }

        public Task<ProviderExecutionResult> ExecuteAsync(
            ProviderExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (_steps.Count == 0)
            {
                throw new InvalidOperationException($"No configured step for provider '{ProviderKey}'.");
            }

            var step = _steps.Dequeue();

            return step switch
            {
                ProviderExecutionResult result => Task.FromResult(result),
                Exception exception => Task.FromException<ProviderExecutionResult>(exception),
                _ => throw new InvalidOperationException("Unsupported step type.")
            };
        }
    }

    private sealed class FakeProviderExecutionGatewayProvider : IProviderExecutionGatewayProvider
    {
        private readonly Dictionary<string, IProviderExecutionGateway> _gateways;

        public FakeProviderExecutionGatewayProvider(params IProviderExecutionGateway[] gateways)
        {
            _gateways = gateways.ToDictionary(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase);
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