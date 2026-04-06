using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using KnownSummaries = AIL.Modules.Decision.Domain.KnownMemoryInfluenceSummaries;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryStoreTests
{
    private static DecisionHistoryRecord SampleRecord(
        Guid tenantId,
        string decisionType = "t1",
        string strategy = "default_safe",
        Guid? correlationGroupId = null,
        string? memoryInfluenceSummary = null,
        Guid? executionInstanceId = null)
    {
        var id = Guid.NewGuid();
        return new DecisionHistoryRecord(
            Id: id,
            TenantId: tenantId,
            CorrelationGroupId: correlationGroupId,
            ExecutionInstanceId: executionInstanceId,
            DecisionType: decisionType,
            SubjectType: "st",
            SubjectId: "sid",
            SelectedStrategyKey: strategy,
            SelectedOptionId: strategy,
            ConfidenceTier: DecisionConfidence.Low.ToString(),
            PolicyKey: decisionType,
            ReasonSummary: "r",
            ConsideredStrategies: new[] { strategy },
            UsedMemory: false,
            MemoryItemCount: 0,
            MemoryInfluenceSummary: memoryInfluenceSummary ?? KnownSummaries.NoMemory,
            Options: new[]
            {
                new DecisionHistoryOptionSnapshot(strategy, DecisionConfidence.Low.ToString(), 0.1, "x")
            },
            Outcome: "Succeeded",
            CreatedAtUtc: DateTime.UtcNow);
    }

    [Fact]
    public void TryGet_ReturnsNull_WhenTenantMismatch()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var r = SampleRecord(tenantA);
        store.Put(r);

        Assert.Null(store.TryGet(tenantB, r.Id));
        Assert.NotNull(store.TryGet(tenantA, r.Id));
    }

    [Fact]
    public void List_FiltersByTenantAndDecisionType_AndPages()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        store.Put(SampleRecord(tenant, "alpha"));
        store.Put(SampleRecord(tenant, "beta"));
        store.Put(SampleRecord(tenant, "beta"));

        var q = new DecisionHistoryListQuery(tenant, Page: 1, PageSize: 1, DecisionType: "beta");
        var (page1, total) = store.List(q);
        Assert.Equal(2, total);
        Assert.Single(page1);
        Assert.Equal("beta", page1[0].DecisionType);

        var (page2, _) = store.List(new DecisionHistoryListQuery(tenant, Page: 2, PageSize: 1, DecisionType: "beta"));
        Assert.Single(page2);
    }

    [Fact]
    public void List_ClampPageSize_ToMax100()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        for (var i = 0; i < 120; i++)
            store.Put(SampleRecord(tenant));

        var (slice, total) = store.List(new DecisionHistoryListQuery(tenant, Page: 1, PageSize: 500));
        Assert.Equal(120, total);
        Assert.Equal(100, slice.Count);
    }

    [Fact]
    public void List_FiltersByCorrelationGroupId_ExactMatch()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        var cgA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var cgB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        store.Put(SampleRecord(tenant, correlationGroupId: cgA));
        store.Put(SampleRecord(tenant, correlationGroupId: cgB));

        var q = new DecisionHistoryListQuery(tenant, 1, 10, CorrelationGroupId: cgA);
        var (items, total) = store.List(q);
        Assert.Equal(1, total);
        Assert.Equal(cgA, items[0].CorrelationGroupId);
    }

    [Fact]
    public void List_FiltersByMemoryInfluenceSummary_CombinedWithCorrelation()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        var cg = Guid.NewGuid();
        store.Put(SampleRecord(tenant, memoryInfluenceSummary: KnownSummaries.MemoryEmpty, correlationGroupId: cg));
        store.Put(SampleRecord(tenant, memoryInfluenceSummary: KnownSummaries.NoMemory, correlationGroupId: cg));

        var q = new DecisionHistoryListQuery(
            tenant,
            1,
            10,
            CorrelationGroupId: cg,
            MemoryInfluenceSummary: KnownSummaries.MemoryEmpty);
        var (items, total) = store.List(q);
        Assert.Equal(1, total);
        Assert.Equal(KnownSummaries.MemoryEmpty, items[0].MemoryInfluenceSummary);
    }

    [Fact]
    public void List_FilterByCorrelation_ExcludesNullCorrelationRows()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        var cg = Guid.NewGuid();
        store.Put(SampleRecord(tenant, correlationGroupId: null));
        store.Put(SampleRecord(tenant, correlationGroupId: cg));

        var (items, total) = store.List(new DecisionHistoryListQuery(tenant, 1, 10, CorrelationGroupId: cg));
        Assert.Equal(1, total);
        Assert.Equal(cg, items[0].CorrelationGroupId);
    }

    [Fact]
    public void List_FiltersByExecutionInstanceId_ExactMatch()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        var exA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var exB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        store.Put(SampleRecord(tenant, executionInstanceId: exA));
        store.Put(SampleRecord(tenant, executionInstanceId: exB));

        var q = new DecisionHistoryListQuery(tenant, 1, 10, ExecutionInstanceId: exA);
        var (items, total) = store.List(q);
        Assert.Equal(1, total);
        Assert.Equal(exA, items[0].ExecutionInstanceId);
    }

    [Fact]
    public void List_FilterByExecutionInstance_CombinedWithCorrelation()
    {
        var store = new InMemoryDecisionHistoryStore();
        var tenant = Guid.NewGuid();
        var cg = Guid.NewGuid();
        var ex = Guid.NewGuid();
        store.Put(SampleRecord(tenant, correlationGroupId: cg, executionInstanceId: ex));
        store.Put(SampleRecord(tenant, correlationGroupId: cg, executionInstanceId: Guid.NewGuid()));

        var (items, total) = store.List(
            new DecisionHistoryListQuery(tenant, 1, 10, CorrelationGroupId: cg, ExecutionInstanceId: ex));
        Assert.Equal(1, total);
    }
}
