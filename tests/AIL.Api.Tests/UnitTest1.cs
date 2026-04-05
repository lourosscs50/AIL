using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

public class ExecuteIntelligenceEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExecuteIntelligenceEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_ExecuteIntelligence_Returns200_OnAllowedTenant()
    {
        var client = _factory.CreateClient();

        var request = new ExecuteIntelligenceRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string> { "ref1" });

        var response = await client.PostAsJsonAsync("/execute-intelligence", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ExecuteIntelligenceResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.OutputText);
        Assert.NotEqual(Guid.Empty, body.AuditRecordId);
        Assert.NotEqual(Guid.Empty, body.ExecutionInstanceId);
        Assert.NotNull(body.DecisionVisibility);
        var dv = body.DecisionVisibility!;
        Assert.Equal(body.ExecutionInstanceId, dv.DecisionId);
        Assert.Equal(body.ExecutionInstanceId, dv.Trace.ExecutionInstanceId);
        Assert.Equal(AilDecisionVisibilityKeys.StatusSucceeded, dv.Status);
        Assert.Equal(AilDecisionVisibilityKeys.CategoryExecution, dv.DecisionCategory);
        Assert.NotNull(dv.ExecutionExtension);
    }

    [Fact]
    public async Task Post_ExecuteIntelligence_Returns403_OnDeniedTenant()
    {
        var client = _factory.CreateClient();

        var request = new ExecuteIntelligenceRequest(
            TenantId: Guid.Empty,
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>());

        var response = await client.PostAsJsonAsync("/execute-intelligence", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Executions_Returns404_WhenUnknown()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/executions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ThenGet_Executions_ReturnsAlignedVisibility()
    {
        var client = _factory.CreateClient();
        var execId = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var entity = Guid.NewGuid();

        var request = new ExecuteIntelligenceRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string> { entity.ToString() },
            IncludeMemory: false,
            MemoryQuery: null,
            ExecutionInstanceId: execId,
            TraceThreadId: "w3c-trace-abc",
            CorrelationGroupId: correlation);

        var post = await client.PostAsJsonAsync("/execute-intelligence", request);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var get = await client.GetAsync($"/executions/{execId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var vis = await get.Content.ReadFromJsonAsync<DecisionVisibilityResponse>();
        Assert.NotNull(vis);
        Assert.Equal(execId, vis!.DecisionId);
        Assert.Equal(execId, vis.Trace.ExecutionInstanceId);
        Assert.Equal("w3c-trace-abc", vis.Trace.TraceId);
        Assert.Equal(correlation, vis.Trace.CorrelationId);
        Assert.Equal(entity, Assert.Single(vis.Trace.RelatedEntityIds));
        Assert.Equal("prompt", vis.ExecutionExtension!.Prompt.PromptKey);
    }

    [Fact]
    public async Task Post_Forbidden_ExposesExecutionInstanceIdHeader_AndGetReturnsDenied()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/execute-intelligence",
            new ExecuteIntelligenceRequest(
                TenantId: Guid.Empty,
                CapabilityKey: "capability",
                PromptKey: "prompt",
                Variables: new Dictionary<string, string>(),
                ContextReferenceIds: new List<string>()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Ail-Execution-Instance-Id", out var values));
        var id = Guid.Parse(Assert.Single(values));

        var get = await client.GetAsync($"/executions/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var vis = await get.Content.ReadFromJsonAsync<DecisionVisibilityResponse>();
        Assert.Equal(AilDecisionVisibilityKeys.StatusDenied, vis!.Status);
    }

    [Fact]
    public async Task Visibility_Json_DoesNotLeakPromptTemplateOrMemoryPayloadFields()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsJsonAsync(
            "/execute-intelligence",
            new ExecuteIntelligenceRequest(
                TenantId: Guid.NewGuid(),
                CapabilityKey: "capability",
                PromptKey: "prompt",
                Variables: new Dictionary<string, string> { ["k"] = "v" },
                ContextReferenceIds: new List<string>()));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var raw = await post.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        Assert.False(doc.RootElement.TryGetProperty("promptText", out _));
        Assert.False(doc.RootElement.TryGetProperty("promptTemplate", out _));
        Assert.False(doc.RootElement.TryGetProperty("memoryContext", out _));
        Assert.True(doc.RootElement.TryGetProperty("decisionVisibility", out var dv));
        Assert.True(dv.TryGetProperty("trace", out var tr));
        Assert.True(
            !tr.TryGetProperty("executionId", out var legacyEx) || legacyEx.ValueKind == JsonValueKind.Null,
            "SignalForge legacy executionId slot must be absent or null for A.I.L.");
        Assert.NotEqual(Guid.Empty, tr.GetProperty("executionInstanceId").GetGuid());
    }
}
