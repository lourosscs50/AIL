using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using AIL.Api;
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
            CorrelationGroupId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            ExecutionInstanceId: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));

        var post = await client.PostAsJsonAsync("/decisions", postReq);
        post.EnsureSuccessStatusCode();
        var decided = await post.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(decided?.DecisionRecordId);
        Assert.Equal(postReq.CorrelationGroupId, decided!.CorrelationGroupId);
        Assert.Equal(postReq.ExecutionInstanceId, decided.ExecutionInstanceId);

        var getUrl = $"/decisions/history/{decided!.DecisionRecordId}?tenantId={tenant:D}";
        var get = await client.GetAsync(getUrl);
        get.EnsureSuccessStatusCode();
        var item = await get.Content.ReadFromJsonAsync<DecisionHistoryItemResponse>();
        Assert.NotNull(item);
        Assert.Equal(decided.DecisionRecordId, item!.Id);
        Assert.NotEqual(item.Id, item.CorrelationGroupId);
        Assert.NotEqual(item.Id, item.ExecutionInstanceId);
        Assert.NotEqual(item.CorrelationGroupId, item.ExecutionInstanceId);
        Assert.Equal(tenant, item.TenantId);
        Assert.Equal(postReq.CorrelationGroupId, item.CorrelationGroupId);
        Assert.Equal(postReq.ExecutionInstanceId, item.ExecutionInstanceId);
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
        Assert.Equal(DecisionEndpointMapping.DecisionHistorySortByCreatedAtUtc, page.SortBy);
        Assert.Equal(DecisionEndpointMapping.DecisionHistorySortDirectionDesc, page.SortDirection);
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

    [Fact]
    public async Task Get_DecisionHistory_List_FiltersByCorrelationGroupId()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var cgA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var cgB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        for (var i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/decisions", new DecideRequest(
                tenant,
                "alpha_route",
                "s",
                $"a{i}",
                null,
                null,
                false,
                null,
                null,
                null,
                cgA));
        }

        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "b0",
            null,
            null,
            false,
            null,
            null,
            null,
            cgB));

        var url =
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={cgA:D}&page=1&pageSize=10";
        var list = await client.GetAsync(url);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, x => Assert.Equal(cgA, x.CorrelationGroupId));
    }

    [Fact]
    public async Task Get_DecisionHistory_List_CombinedCorrelationAndDecisionType_AndPaging()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var cg = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        for (var i = 0; i < 3; i++)
        {
            var dt = i == 0 ? "gamma_route" : "delta_route";
            await client.PostAsJsonAsync("/decisions", new DecideRequest(
                tenant,
                dt,
                "s",
                $"c{i}",
                null,
                null,
                false,
                null,
                null,
                null,
                cg));
        }

        var url =
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={cg:D}&decisionType=delta_route&page=1&pageSize=1";
        var p1 = await client.GetAsync(url);
        p1.EnsureSuccessStatusCode();
        var page1 = await p1.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.TotalCount);
        Assert.Single(page1.Items);
        Assert.Equal("delta_route", page1.Items[0].DecisionType);

        var p2 = await client.GetAsync(
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={cg:D}&decisionType=delta_route&page=2&pageSize=1");
        p2.EnsureSuccessStatusCode();
        var page2 = await p2.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page2);
        Assert.Equal(2, page2!.TotalCount);
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_FiltersByMemoryInfluenceSummary()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var cg = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "control_trigger_routing",
            "control_trigger",
            "mem-empty",
            null,
            null,
            true,
            new DecideMemoryQueryRequest("Tenant", null, "Fact"),
            null,
            null,
            cg));
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "control_trigger_routing",
            "control_trigger",
            "no-mem",
            null,
            null,
            false,
            null,
            null,
            null,
            cg));

        var url =
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={cg:D}&memoryInfluenceSummary=memory_empty&page=1&pageSize=10";
        var list = await client.GetAsync(url);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("memory_empty", page.Items[0].MemoryInfluenceSummary);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_EmptyCorrelationGroupId_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var url =
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={Guid.Empty:D}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_MemoryInfluenceSummaryTooLong_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var tooLong = new string('x', DecisionEndpointMapping.MaxMemoryInfluenceSummaryFilterLength + 1);
        var url =
            $"/decisions/history?tenantId={tenant:D}&memoryInfluenceSummary={Uri.EscapeDataString(tooLong)}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_FiltersByExecutionInstanceId()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var exA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var exB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "e0",
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            exA));
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "e1",
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            exA));
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "e2",
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            exB));

        var url = $"/decisions/history?tenantId={tenant:D}&executionInstanceId={exA:D}&page=1&pageSize=10";
        var list = await client.GetAsync(url);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, x => Assert.Equal(exA, x.ExecutionInstanceId));
    }

    [Fact]
    public async Task Get_DecisionHistory_List_EmptyExecutionInstanceId_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var url =
            $"/decisions/history?tenantId={tenant:D}&executionInstanceId={Guid.Empty:D}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_InvalidSortBy_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var url =
            $"/decisions/history?tenantId={tenant:D}&sortBy=invalidSort";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_OmittedPageAndPageSize_DefaultsTo1And50()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "p0",
            null,
            null,
            false,
            null,
            null,
            null,
            null));

        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}");
        res.EnsureSuccessStatusCode();
        var page = await res.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(DecisionEndpointMapping.DefaultDecisionHistoryListPageSize, page.PageSize);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_PageZero_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}&page=0");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_PageSizeAboveMax_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync(
            $"/decisions/history?tenantId={tenant:D}&pageSize={DecisionEndpointMapping.MaxDecisionHistoryListPageSize + 1}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_PageSizeAtMax_IsAccepted()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "pm",
            null,
            null,
            false,
            null,
            null,
            null,
            null));

        var res = await client.GetAsync(
            $"/decisions/history?tenantId={tenant:D}&page=1&pageSize={DecisionEndpointMapping.MaxDecisionHistoryListPageSize}");
        res.EnsureSuccessStatusCode();
        var page = await res.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(DecisionEndpointMapping.MaxDecisionHistoryListPageSize, page!.PageSize);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_DecisionTypeFilterTooLong_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var tooLong = new string('z', DecisionEndpointMapping.MaxDecisionHistoryDecisionTypeFilterLength + 1);
        var url =
            $"/decisions/history?tenantId={tenant:D}&decisionType={Uri.EscapeDataString(tooLong)}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_MalformedCorrelationGroupId_Returns400()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}&correlationGroupId=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_SortDirectionAsc_IsAccepted_AndEchoed()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "x",
            null,
            null,
            false,
            null,
            null,
            null,
            null));

        var url =
            $"/decisions/history?tenantId={tenant:D}&sortBy=createdAtUtc&sortDirection=asc";
        var list = await client.GetAsync(url);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        Assert.Equal(DecisionEndpointMapping.DecisionHistorySortByCreatedAtUtc, page!.SortBy);
        Assert.Equal(DecisionEndpointMapping.DecisionHistorySortDirectionAsc, page.SortDirection);
    }

    [Fact]
    public async Task Get_DecisionHistory_List_AndDetail_ShareCoreFields_DetailHasRationaleAndOptions()
    {
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var post = await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "control_trigger_routing",
            "control_trigger",
            "subj",
            null,
            null,
            false,
            null,
            null,
            null,
            null));
        post.EnsureSuccessStatusCode();
        var decided = await post.Content.ReadFromJsonAsync<DecideResponse>();
        Assert.NotNull(decided?.DecisionRecordId);
        var id = decided!.DecisionRecordId!.Value;

        var listUrl = $"/decisions/history?tenantId={tenant:D}&decisionType=control_trigger_routing&page=1&pageSize=10";
        var list = await client.GetAsync(listUrl);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedDecisionHistoryResponse>();
        Assert.NotNull(page);
        var row = Assert.Single(page!.Items, i => i.Id == id);
        Assert.Equal(decided.PolicyKey, row.PolicyKey);
        Assert.Equal(decided.SelectedStrategyKey, row.SelectedStrategyKey);
        Assert.Equal(decided.MemoryInfluenceSummary, row.MemoryInfluenceSummary);

        var get = await client.GetAsync($"/decisions/history/{id:D}?tenantId={tenant:D}");
        get.EnsureSuccessStatusCode();
        var detail = await get.Content.ReadFromJsonAsync<DecisionHistoryItemResponse>();
        Assert.NotNull(detail);
        Assert.Equal(row.Id, detail!.Id);
        Assert.Equal(row.PolicyKey, detail.PolicyKey);
        Assert.Equal(row.CreatedAtUtc, detail.CreatedAtUtc);
        Assert.NotNull(detail.ReasonSummary);
        Assert.True(detail.Options.Count > 0);
    }
}
