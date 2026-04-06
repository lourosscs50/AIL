using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using KnownSummaries = AIL.Modules.Decision.Domain.KnownMemoryInfluenceSummaries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIL.Modules.Decision.Tests;

public sealed class EfDecisionHistoryStoreTests : IDisposable
{
    private readonly string _dbPath;

    public EfDecisionHistoryStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ail_ef_hist_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-shm");
        TryDelete(_dbPath + "-wal");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // test cleanup best-effort
        }
    }

    private IDecisionHistoryStore CreateStore()
    {
        var config = new ConfigurationBuilder()
            .Add(new MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?>
                {
                    ["DecisionHistory:SqliteConnectionString"] = $"Data Source={_dbPath}",
                },
            })
            .Build();
        var services = new ServiceCollection();
        services.AddDecisionHistoryStore(config);
        return services.BuildServiceProvider().GetRequiredService<IDecisionHistoryStore>();
    }

    private static DecisionHistoryRecord Sample(
        Guid tenantId,
        Guid id,
        DateTime createdAtUtc,
        string decisionType = "dtype",
        Guid? correlationGroupId = null,
        Guid? executionInstanceId = null,
        string memorySummary = "")
    {
        memorySummary = string.IsNullOrEmpty(memorySummary) ? KnownSummaries.NoMemory : memorySummary;
        return new DecisionHistoryRecord(
            Id: id,
            TenantId: tenantId,
            CorrelationGroupId: correlationGroupId,
            ExecutionInstanceId: executionInstanceId,
            DecisionType: decisionType,
            SubjectType: "st",
            SubjectId: "sid",
            SelectedStrategyKey: "default_safe",
            SelectedOptionId: "default_safe",
            ConfidenceTier: DecisionConfidence.Low.ToString(),
            PolicyKey: decisionType,
            ReasonSummary: "r",
            ConsideredStrategies: new[] { "default_safe" },
            UsedMemory: false,
            MemoryItemCount: 0,
            MemoryInfluenceSummary: memorySummary,
            Options: new[]
            {
                new DecisionHistoryOptionSnapshot("default_safe", DecisionConfidence.Low.ToString(), 0.1, "x")
            },
            Outcome: "Succeeded",
            CreatedAtUtc: createdAtUtc);
    }

    [Fact]
    public void Put_TryGet_RoundTrip_PersistsBoundedFields()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var at = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var r = Sample(tenant, id, at);
        store.Put(r);
        var got = store.TryGet(tenant, id);
        Assert.NotNull(got);
        Assert.Equal(r.Id, got!.Id);
        Assert.Equal(r.ReasonSummary, got.ReasonSummary);
        Assert.Equal(r.Options.Count, got.Options.Count);
        Assert.Equal(r.ConsideredStrategies.Count, got.ConsideredStrategies.Count);
    }

    [Fact]
    public void TryGet_WrongTenant_ReturnsNull()
    {
        var store = CreateStore();
        var tenantA = Guid.NewGuid();
        var id = Guid.NewGuid();
        store.Put(Sample(tenantA, id, DateTime.UtcNow));
        Assert.Null(store.TryGet(Guid.NewGuid(), id));
    }

    [Fact]
    public void List_TotalAndFilters_MatchInMemorySemantics_ForSameData()
    {
        var tenant = Guid.NewGuid();
        var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var id1 = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("60000000-0000-0000-0000-000000000002");
        var cg = Guid.Parse("70000000-0000-0000-0000-000000000001");

        var mem = new InMemoryDecisionHistoryStore();
        var ef = CreateStore();

        foreach (var store in new IDecisionHistoryStore[] { mem, ef })
        {
            store.Put(Sample(tenant, id1, t1, decisionType: "alpha"));
            store.Put(Sample(tenant, id2, t2, decisionType: "beta", correlationGroupId: cg));

            var q = new DecisionHistoryListQuery(
                tenant,
                1,
                10,
                DecisionType: "beta",
                CorrelationGroupId: cg,
                SortBy: DecisionHistorySortBy.CreatedAtUtc,
                SortDirection: DecisionHistorySortDirection.Descending);
            var (items, total) = store.List(q);
            Assert.Equal(1, total);
            Assert.Single(items);
            Assert.Equal(id2, items[0].Id);
        }
    }

    [Fact]
    public void List_InclusiveDateRange_Applies()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var low = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var mid = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var high = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        store.Put(Sample(tenant, Guid.NewGuid(), low));
        store.Put(Sample(tenant, Guid.NewGuid(), mid));
        store.Put(Sample(tenant, Guid.NewGuid(), high));

        var q = new DecisionHistoryListQuery(
            tenant,
            1,
            10,
            CreatedFromUtc: mid,
            CreatedToUtc: mid,
            SortBy: DecisionHistorySortBy.CreatedAtUtc,
            SortDirection: DecisionHistorySortDirection.Ascending);
        var (items, total) = store.List(q);
        Assert.Equal(1, total);
        Assert.Equal(mid, items[0].CreatedAtUtc);
    }

    [Fact]
    public void Put_UpsertSameId_ReplacesRow()
    {
        var store = CreateStore();
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var r1 = Sample(tenant, id, DateTime.UtcNow, decisionType: "v1");
        var r2 = Sample(tenant, id, DateTime.UtcNow, decisionType: "v2");
        store.Put(r1);
        store.Put(r2);
        var got = store.TryGet(tenant, id);
        Assert.Equal("v2", got!.DecisionType);
    }
}
