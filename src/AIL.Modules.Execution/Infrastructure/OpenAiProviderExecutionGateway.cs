using AIL.Modules.Execution.Application;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class OpenAiProviderExecutionGateway : IProviderExecutionGateway
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public string ProviderKey => "openai";

    public OpenAiProviderExecutionGateway(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ProviderExecutionResult> ExecuteAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = BuildRequest(request);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        // Minimal response parsing; in real usage you might map more fields.
        var choice = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        var usage = doc.RootElement.GetProperty("usage");

        int? inputTokens = null;
        if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
        {
            inputTokens = promptTokens.GetInt32();
        }

        int? outputTokens = null;
        if (usage.TryGetProperty("completion_tokens", out var completionTokens))
        {
            outputTokens = completionTokens.GetInt32();
        }

        var modelKey = request.PrimaryModelKey ?? _options.Model;

        return new ProviderExecutionResult(
            ProviderKey: ProviderKey,
            ModelKey: modelKey,
            OutputText: choice,
            UsedFallback: false,
            InputTokenCount: inputTokens,
            OutputTokenCount: outputTokens);
    }

    internal HttpRequestMessage BuildRequest(ProviderExecutionRequest request)
    {
        var model = request.PrimaryModelKey ?? _options.Model;

        var payload = new
        {
            model,
            max_tokens = request.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = request.ContextText },
                new { role = "user", content = request.PromptText }
            }
        };

        var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.BaseUrl), "chat/completions"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Content = JsonContent.Create(payload);
        return message;
    }
}
