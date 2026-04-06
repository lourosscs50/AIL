using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryStoreTests
{
    private static DecisionHistoryRecord SampleRecord(Guid tenantId, string decisionType = "t1", string strategy = "default_safe")
    {
        var id = Guid.NewGuid();
        return new DecisionHistoryRecord(
            Id: id,
            TenantId: tenantId,
            CorrelationGroupId: null,
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
}
