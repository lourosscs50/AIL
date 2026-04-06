using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

public sealed class DecisionHistoryEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DecisionHistoryEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_DecisionHistory_ById_AfterPost_ReturnsSameTruth()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var postReq = new DecideRequest(
            TenantId: tenant,
            DecisionType: "control_trigger_routing",
            SubjectType: "control_trigger",
            SubjectId: "hist-subj",
            ContextText: "sensitive-operator-input",
            StructuredContext: new Dictionary<string, string> { ["k"] = "v" },
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: null,
            CorrelationGroupId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

        var post = await client.PostAsJsonAsync("/decisions", postReq);
        post.EnsureSuccessStatusCode();
        var decided = await post.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(decided?.DecisionRecordId);

        var getUrl = $"/decisions/history/{decided!.DecisionRecordId}?tenantId={tenant:D}";
        var get = await client.GetAsync(getUrl);
        get.EnsureSuccessStatusCode();
        var item = await get.Content.ReadFromJsonAsync<DecisionHistoryItemResponse>();
        Assert.NotNull(item);
        Assert.Equal(decided.DecisionRecordId, item!.Id);
        Assert.Equal(tenant, item.TenantId);
        Assert.Equal(postReq.CorrelationGroupId, item.CorrelationGroupId);
        Assert.Equal("control_trigger_routing", item.DecisionType);
        Assert.Equal(decided.SelectedStrategyKey, item.SelectedStrategyKey);
        Assert.Equal(decided.PolicyKey, item.PolicyKey);
        Assert.Equal(decided.MemoryInfluenceSummary, item.MemoryInfluenceSummary);
        Assert.Equal("hist-subj", item.SubjectId);
        Assert.DoesNotContain("sensitive", item.ReasonSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(item.Options.Count > 0);
    }

    [Fact]
    public async Task Get_DecisionHistory_ById_WrongTenant_Returns404()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var postReq = new DecideRequest(
            tenant,
            "control_trigger_routing",
            "control_trigger",
            "x",
            null,
            null,
            false,
            null,
            null,
            null,
            null);

        var post = await client.PostAsJsonAsync("/decisions", postReq);
        post.EnsureSuccessStatusCode();
        var decided = await post.Content.ReadFromJsonAsync<DecideResponse>();
        var id = decided!.DecisionRecordId!.Value;

        var otherTenant = Guid.NewGuid();
        var get = await client.GetAsync($"/decisions/history/{id}?tenantId={otherTenant:D}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_FiltersByDecisionType_AndBoundsPaging()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            var dt = i == 0 ? "alpha_route" : "beta_route";
            await client.PostAsJsonAsync("/decisions", new DecideRequest(
                tenant,
                dt,
                "s",
                $"id{i}",
                null,
                null,
                false,
                null,
                null,
                null,
                null));
        }

        var listUrl = $"/decisions/history?tenantId={tenant:D}&decisionType=beta_route&page=1&pageSize=10";
        var list = await client.GetAsync(listUrl);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, x => Assert.Equal("beta_route", x.DecisionType));
        Assert.True(page.Items.Count <= 10);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_InvalidRange_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var url =
            $"/decisions/history?tenantId={tenant:D}&fromUtc=2026-12-01T00:00:00Z&toUtc=2026-01-01T00:00:00Z";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_MissingTenant_Returns400()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/decisions/history");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
