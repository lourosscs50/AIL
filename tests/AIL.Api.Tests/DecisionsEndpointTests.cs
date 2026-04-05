using System.Net;
using System.Net.Http.Json;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

public sealed class DecisionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DecisionsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_Decisions_Returns200_And_Normalized_Strategy_Key()
    {
        var client = _factory.CreateClient();
        var request = new DecideRequest(
            TenantId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: Guid.NewGuid().ToString("D"),
            ContextText: null,
            StructuredContext: new Dictionary<string, string>
            {
                ["lifecycle_event_type"] = "AlertCreated",
                ["trigger_type"] = "AlertCreated"
            },
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null);

        var response = await client.PostAsJsonAsync("/decisions", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(body);
        Assert.Equal("control_trigger_routing", body!.DecisionType);
        Assert.Equal("default_safe", body.SelectedStrategyKey);
        Assert.False(body.UsedMemory);
        Assert.NotEmpty(body.Options);
    }

    [Fact]
    public async Task Post_Decisions_Escalation_Context_Selects_ContextEscalated()
    {
        var client = _factory.CreateClient();
        var request = new DecideRequest(
            TenantId: Guid.NewGuid(),
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: "subj",
            ContextText: null,
            StructuredContext: new Dictionary<string, string> { ["escalation"] = "true" },
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null);

        var response = await client.PostAsJsonAsync("/decisions", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(body);
        Assert.Equal("context_escalated", body!.SelectedStrategyKey);
    }

    [Fact]
    public async Task Post_Decisions_Invalid_Tenant_Returns400()
    {
        var client = _factory.CreateClient();
        var request = new DecideRequest(
            TenantId: Guid.Empty,
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: "x",
            ContextText: null,
            StructuredContext: null,
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null);

        var response = await client.PostAsJsonAsync("/decisions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
