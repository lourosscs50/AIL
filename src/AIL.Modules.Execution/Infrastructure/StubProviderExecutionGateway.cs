using AIL.Modules.Execution.Application;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class StubProviderExecutionGateway : IProviderExecutionGateway
{
    public string ProviderKey => "stub-provider";

    public Task<ProviderExecutionResult> ExecuteAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var outputText = $"[provider-stub] Executed {request.CapabilityKey} using prompt {request.PromptKey} {request.PromptVersion}; " +
                         $"primaryProvider={request.PrimaryProviderKey} primaryModel={request.PrimaryModelKey} " +
                         $"fallbackProvider={request.FallbackProviderKey} fallbackModel={request.FallbackModelKey} " +
                         $"maxTokens={request.MaxTokens} fallbackAllowed={request.FallbackAllowed}; " +
                         $"context={request.ContextText} metadata={string.Join(";", request.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}";

        return Task.FromResult(new ProviderExecutionResult(
            ProviderKey: request.PrimaryProviderKey ?? ProviderKey,
            ModelKey: request.PrimaryModelKey ?? "stub-model",
            OutputText: outputText,
            UsedFallback: false,
            InputTokenCount: null,
            OutputTokenCount: null));
    }
}
