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
    /// <summary>Default in-memory implementation; tests assert behavior through <see cref="IDecisionHistoryStore"/> only.</summary>
    private static IDecisionHistoryStore CreateStore(DecisionHistoryRetentionOptions? options = null) =>
        new InMemoryDecisionHistoryStore(options);

    private static DecisionHistoryRecord SampleRecord(
        Guid tenantId,
        string decisionType = "t1",
        string strategy = "default_safe",
        Guid? correlationGroupId = null,
        string? memoryInfluenceSummary = null,
        Guid? executionInstanceId = null,
        DateTime? createdAtUtc = null,
        Guid? fixedId = null)
    {
        var id = fixedId ?? Guid.NewGuid();
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
            CreatedAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    [Fact]
    public void TryGet_ReturnsNull_WhenTenantMismatch()
    {
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
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
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var cg = Guid.NewGuid();
        var ex = Guid.NewGuid();
        store.Put(SampleRecord(tenant, correlationGroupId: cg, executionInstanceId: ex));
        store.Put(SampleRecord(tenant, correlationGroupId: cg, executionInstanceId: Guid.NewGuid()));

        var (items, total) = store.List(
            new DecisionHistoryListQuery(tenant, 1, 10, CorrelationGroupId: cg, ExecutionInstanceId: ex));
        Assert.Equal(1, total);
    }

    [Fact]
    public void List_SortsByCreatedAtUtc_Descending_ByDefault_TieBreakById()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var tEarly = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tLate = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        store.Put(SampleRecord(tenant, createdAtUtc: tEarly, fixedId: idA));
        store.Put(SampleRecord(tenant, createdAtUtc: tLate, fixedId: idB));

        var (items, total) = store.List(new DecisionHistoryListQuery(tenant, 1, 10));
        Assert.Equal(2, total);
        Assert.Equal(idB, items[0].Id);
        Assert.Equal(idA, items[1].Id);
    }

    [Fact]
    public void List_SortsByCreatedAtUtc_Ascending_TieBreakById()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var idLo = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var idHi = Guid.Parse("22222222-2222-2222-2222-222222222222");
        store.Put(SampleRecord(tenant, createdAtUtc: t1, fixedId: idHi));
        store.Put(SampleRecord(tenant, createdAtUtc: t1, fixedId: idLo));

        var q = new DecisionHistoryListQuery(
            tenant,
            1,
            10,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending);
        var (items, _) = store.List(q);
        Assert.Equal(idLo, items[0].Id);
        Assert.Equal(idHi, items[1].Id);
    }

    [Fact]
    public void List_PagingStable_UnderAscendingSort()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var t = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id3));
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id1));
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id2));

        var q = new DecisionHistoryListQuery(
            tenant,
            1,
            2,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending);
        var (page1, total) = store.List(q);
        Assert.Equal(3, total);
        Assert.Equal(2, page1.Count);
        Assert.Equal(id1, page1[0].Id);
        Assert.Equal(id2, page1[1].Id);

        var (page2, _) = store.List(new DecisionHistoryListQuery(
            tenant,
            2,
            2,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending));
        Assert.Single(page2);
        Assert.Equal(id3, page2[0].Id);
    }

    [Fact]
    public void Put_EvictsOldestInserted_WhenAtCapacity_Fifo()
    {
        var store = CreateStore(new DecisionHistoryRetentionOptions { MaxRetainedRecords = 3 });
        var tenant = Guid.NewGuid();
        var id1 = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var id4 = Guid.Parse("30000000-0000-0000-0000-000000000004");
        store.Put(SampleRecord(tenant, fixedId: id1));
        store.Put(SampleRecord(tenant, fixedId: id2));
        store.Put(SampleRecord(tenant, fixedId: id3));
        Assert.NotNull(store.TryGet(tenant, id1));
        store.Put(SampleRecord(tenant, fixedId: id4));
        Assert.Null(store.TryGet(tenant, id1));
        Assert.NotNull(store.TryGet(tenant, id2));
        Assert.NotNull(store.TryGet(tenant, id3));
        Assert.NotNull(store.TryGet(tenant, id4));
    }

    [Fact]
    public void List_TotalCount_ReflectsOnlyRetained_AfterEviction()
    {
        var store = CreateStore(new DecisionHistoryRetentionOptions { MaxRetainedRecords = 2 });
        var tenant = Guid.NewGuid();
        var id1 = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("40000000-0000-0000-0000-000000000003");
        store.Put(SampleRecord(tenant, fixedId: id1));
        store.Put(SampleRecord(tenant, fixedId: id2));
        store.Put(SampleRecord(tenant, fixedId: id3));
        var (items, total) = store.List(new DecisionHistoryListQuery(tenant, 1, 10));
        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, r => r.Id == id1);
    }

    [Fact]
    public void List_Paging_AfterEviction_NoSkippedPages()
    {
        var store = CreateStore(new DecisionHistoryRetentionOptions { MaxRetainedRecords = 3 });
        var tenant = Guid.NewGuid();
        var t = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var id1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("50000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("50000000-0000-0000-0000-000000000003");
        var id4 = Guid.Parse("50000000-0000-0000-0000-000000000004");
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id1));
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id2));
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id3));
        store.Put(SampleRecord(tenant, createdAtUtc: t, fixedId: id4));
        var (p1, total) = store.List(new DecisionHistoryListQuery(
            tenant,
            1,
            2,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending));
        Assert.Equal(3, total);
        Assert.Equal(2, p1.Count);
        var (p2, _) = store.List(new DecisionHistoryListQuery(
            tenant,
            2,
            2,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending));
        Assert.Single(p2);
    }

    [Fact]
    public void RetentionOptions_MaxRetainedRecords_BelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateStore(new DecisionHistoryRetentionOptions { MaxRetainedRecords = 0 }));
    }
}
