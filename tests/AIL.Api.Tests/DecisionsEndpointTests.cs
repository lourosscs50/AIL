using System.Linq;
using System.Net;
using System.Reflection;
using System.Net.Http.Json;
using System.Text.Json;
using AIL.Api;
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
        Assert.Equal("control_trigger_routing", body.PolicyKey);
        Assert.Equal("default_safe", body.SelectedStrategyKey);
        Assert.False(body.UsedMemory);
        Assert.NotEmpty(body.Options);
        AssertSelectedOptionIsExplicitAndInCanonicalOptions(body, "default_safe");
        AssertNoParallelSelectedOptionPayload(body);
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
        AssertSelectedOptionIsExplicitAndInCanonicalOptions(body, "context_escalated");
        AssertNoParallelSelectedOptionPayload(body);
    }

    [Fact]
    public async Task Post_Decisions_Winner_Resolvable_By_SelectedOptionId_Not_List_Position_Alone()
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

        var body = await PostDecisionsAsync(client, request);

        Assert.NotNull(body.SelectedOptionId);
        // Deliberately resolve by SelectedOptionId (not Options[0]) so consumers are not tied to index semantics.
        var resolved = Assert.Single(body.Options, o => o.OptionId == body.SelectedOptionId);
        Assert.Equal(body.SelectedStrategyKey, resolved.OptionId);
    }

    [Fact]
    public async Task Post_Decisions_Option_Order_And_SelectedOptionId_Are_Deterministic_Across_Calls()
    {
        var client = _factory.CreateClient();
        var request = new DecideRequest(
            TenantId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
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

        var first = await PostDecisionsAsync(client, request);
        var second = await PostDecisionsAsync(client, request);

        Assert.Equal(
            first.Options.Select(o => o.OptionId),
            second.Options.Select(o => o.OptionId));
        Assert.Equal(first.SelectedOptionId, second.SelectedOptionId);
        Assert.Equal(first.SelectedStrategyKey, second.SelectedStrategyKey);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.Equal(first.ReasonSummary, second.ReasonSummary);
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

    [Fact]
    public async Task Post_Decisions_StructuredContext_ExceedsMaxEntries_Returns400()
    {
        var client = _factory.CreateClient();
        var oversized = Enumerable.Range(0, DecisionEndpointMapping.MaxStructuredContextEntries + 1)
            .ToDictionary(i => $"k{i}", i => "v");
        var request = new DecideRequest(
            TenantId: Guid.NewGuid(),
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: "x",
            ContextText: null,
            StructuredContext: oversized,
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null);

        var response = await client.PostAsJsonAsync("/decisions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Decisions_Response_DoesNotEcho_ClientMetadata()
    {
        var client = _factory.CreateClient();
        var request = new DecideRequest(
            TenantId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: Guid.NewGuid().ToString("D"),
            ContextText: null,
            StructuredContext: new Dictionary<string, string> { ["lifecycle_event_type"] = "AlertCreated" },
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: new Dictionary<string, string> { ["client_secret"] = "should-not-appear" });

        var response = await client.PostAsJsonAsync("/decisions", request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<DecideResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(body);
        Assert.Null(body!.Metadata);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("prompt", out _));
        Assert.False(doc.RootElement.TryGetProperty("rawModelOutput", out _));
    }

    [Fact]
    public void ValidateDecideRequest_AcceptsBoundarySizedStructuredContext()
    {
        var keys = Enumerable.Range(0, DecisionEndpointMapping.MaxStructuredContextEntries)
            .ToDictionary(i => $"k{i}", _ => "v");
        var req = new DecideRequest(
            TenantId: Guid.NewGuid(),
            DecisionType: "t",
            SubjectType: "s",
            SubjectId: "id",
            ContextText: null,
            StructuredContext: keys,
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null);
        DecisionEndpointMapping.ValidateDecideRequest(req);
    }

    private static async Task<DecideResponse> PostDecisionsAsync(HttpClient client, DecideRequest request)
    {
        var response = await client.PostAsJsonAsync("/decisions", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(body);
        return body!;
    }

    /// <summary>
    /// SelectedOptionId must match an entry in the canonical Options list; SelectedStrategyKey stays the advisory winner key.
    /// </summary>
    private static void AssertSelectedOptionIsExplicitAndInCanonicalOptions(DecideResponse body, string expectedStrategyKey)
    {
        Assert.Equal(expectedStrategyKey, body.SelectedStrategyKey);
        Assert.NotNull(body.SelectedOptionId);
        Assert.Equal(expectedStrategyKey, body.SelectedOptionId);
        _ = Assert.Single(body.Options, o => o.OptionId == body.SelectedOptionId);
    }

    /// <summary>
    /// Only <see cref="DecideResponse.Options"/> carries option rows; no parallel list property exists on the contract.
    /// </summary>
    private static void AssertNoParallelSelectedOptionPayload(DecideResponse body)
    {
        Assert.NotNull(body.Options);
        var optionListProps = typeof(DecideResponse)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(IReadOnlyList<DecideOptionResponse>))
            .Select(p => p.Name)
            .ToList();
        Assert.Single(optionListProps);
        Assert.Equal(nameof(DecideResponse.Options), optionListProps[0]);
    }
}
