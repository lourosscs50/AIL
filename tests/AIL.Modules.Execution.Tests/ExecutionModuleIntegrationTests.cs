using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AIL.Modules.Execution.Tests;

public sealed class ExecutionModuleIntegrationTests
{
    [Fact]
    public void AddExecutionModule_RegistersStubGateway_WhenModeIsStub()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Mode"] = "Stub",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddExecutionModule(configuration);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IProviderExecutionGateway>();

        Assert.Equal("StubProviderExecutionGateway", gateway.GetType().Name);
    }

    [Fact]
    public void AddExecutionModule_RegistersOpenAiGateway_WhenModeIsOpenAi()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Mode"] = "OpenAI",
                ["Providers:OpenAI:ApiKey"] = "test-key",
                ["Providers:OpenAI:BaseUrl"] = "https://api.openai.com/v1",
                ["Providers:OpenAI:Model"] = "gpt-4.1-mini",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddExecutionModule(configuration);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IProviderExecutionGateway>();

        Assert.Equal("OpenAiProviderExecutionGateway", gateway.GetType().Name);
    }

    [Fact]
    public void AddExecutionModule_ThrowsWhenOpenAiEnabledAndApiKeyMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Mode"] = "OpenAI",
            })
            .Build();

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddExecutionModule(configuration));
        Assert.Contains("ApiKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenAiProviderGateway_MapsRequestToOpenAiPayload()
    {
        var options = Options.Create(new OpenAiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-4.1-mini",
        });

        var handler = new CapturingHttpMessageHandler();
        handler.SetResponse(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "hi"
                    }
                }
            },
            usage = new
            {
                prompt_tokens = 1,
                completion_tokens = 2
            }
        });

        var httpClient = new HttpClient(handler);
        var gateway = new OpenAiProviderExecutionGateway(httpClient, options);

        var request = new ProviderExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "prompt",
            PromptVersion: "v1",
            PromptText: "hello",
            ContextText: "ctx",
            MaxTokens: 123,
            FallbackAllowed: true,
            PrimaryProviderKey: "openai",
            PrimaryModelKey: "gpt-4.1-mini",
            FallbackProviderKey: "stub-provider",
            FallbackModelKey: "stub-model",
            Metadata: new Dictionary<string, string> { ["foo"] = "bar" });

        var result = await gateway.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal("hi", result.OutputText);
        Assert.Equal(1, result.InputTokenCount);
        Assert.Equal(2, result.OutputTokenCount);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer test-key", handler.LastRequest!.Headers.Authorization?.ToString());

        Assert.NotNull(handler.LastRequest.Content);
        var body = await handler.LastRequest.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("gpt-4.1-mini", body.GetProperty("model").GetString());
        Assert.Equal(123, body.GetProperty("max_tokens").GetInt32());

        var messages = body.GetProperty("messages");
        Assert.Equal("ctx", messages[0].GetProperty("content").GetString());
        Assert.Equal("hello", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void ExecutionService_HasOnlyInterfaceDependencies()
    {
        var constructor = typeof(ExecutionService).GetConstructors().Single();

        foreach (var parameter in constructor.GetParameters())
        {
            Assert.True(parameter.ParameterType.IsInterface, $"{parameter.Name} is not an interface");
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        private HttpResponseMessage? _response;

        public void SetResponse(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            _response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(JsonDocument.Parse(json).RootElement)
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}