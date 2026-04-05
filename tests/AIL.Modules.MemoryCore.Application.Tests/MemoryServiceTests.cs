using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.MemoryCore.Domain;
using AIL.Modules.MemoryCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AIL.Modules.MemoryCore.Application.Tests;

public class MemoryServiceTests
{
    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; }
    }

    [Fact]
    public async Task WriteMemory_Succeeds_WhenValid()
    {
        var repo = new InMemoryMemoryRepository();
        var now = DateTime.UtcNow;
        var provider = new FakeDateTimeProvider { UtcNow = now };
        var service = new MemoryService(repo, provider);

        var request = new CreateMemoryRequest(
            TenantId: Guid.NewGuid(),
            ScopeType: "Tenant",
            ScopeId: null,
            MemoryKind: "Fact",
            Key: "user.key",
            Content: "Value",
            Metadata: new Dictionary<string, string> { ["a"] = "1" },
            Importance: "Medium",
            Source: "UserInput");

        var response = await service.WriteMemoryAsync(request);

        Assert.Equal(request.TenantId, response.TenantId);
        Assert.Equal("user.key", response.Key);
        Assert.Equal("Value", response.Content);
        Assert.Equal(now, response.CreatedAtUtc);
        Assert.Equal(now, response.UpdatedAtUtc);
    }

    [Fact]
    public async Task WriteMemory_Rejects_InvalidRequest()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var request = new CreateMemoryRequest(
            TenantId: Guid.Empty,
            ScopeType: "Tenant",
            ScopeId: null,
            MemoryKind: "Fact",
            Key: null,
            Content: "Value",
            Metadata: null,
            Importance: "Medium",
            Source: "UserInput");

        await Assert.ThrowsAsync<InvalidMemoryRecordException>(() => service.WriteMemoryAsync(request));
    }

    [Fact]
    public async Task GetMemoryById_ReturnsRecord_ForSameTenant()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantId = Guid.NewGuid();
        var request = new CreateMemoryRequest(tenantId, "Tenant", null, "Fact", null, "Value", null, "Medium", "UserInput");
        var created = await service.WriteMemoryAsync(request);

        var fetched = await service.GetMemoryByIdAsync(tenantId, created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task GetMemoryById_DoesNotReturn_CrossTenant()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var request = new CreateMemoryRequest(tenantA, "Tenant", null, "Fact", null, "Value", null, "Medium", "UserInput");
        var created = await service.WriteMemoryAsync(request);

        var fetched = await service.GetMemoryByIdAsync(tenantB, created.Id);

        Assert.Null(fetched);
    }

    [Fact]
    public async Task UpdateMemory_Succeeds_ForSameTenant()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantId = Guid.NewGuid();
        var createRequest = new CreateMemoryRequest(tenantId, "Tenant", null, "Fact", null, "Value", null, "Medium", "UserInput");
        var created = await service.WriteMemoryAsync(createRequest);

        provider.UtcNow = provider.UtcNow.AddMinutes(1);

        var updateRequest = new UpdateMemoryRequest(tenantId, created.Id, "UpdatedValue", new Dictionary<string, string> { ["x"] = "y" }, "High");
        var updated = await service.UpdateMemoryAsync(updateRequest);

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("UpdatedValue", updated.Content);
        Assert.Equal("y", updated.Metadata["x"]);
        Assert.Equal("High", updated.Importance);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > created.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateMemory_Rejects_InvalidRequest()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantId = Guid.NewGuid();
        var createRequest = new CreateMemoryRequest(tenantId, "Tenant", null, "Fact", null, "Value", null, "Medium", "UserInput");
        var created = await service.WriteMemoryAsync(createRequest);

        await Assert.ThrowsAsync<InvalidMemoryRecordException>(() => service.UpdateMemoryAsync(new UpdateMemoryRequest(tenantId, created.Id, "   ", null, "Medium")));
    }

    [Fact]
    public async Task UpdateMemory_DoesNotUpdate_CrossTenant()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var createRequest = new CreateMemoryRequest(tenantA, "Tenant", null, "Fact", null, "Value", null, "Medium", "UserInput");
        var created = await service.WriteMemoryAsync(createRequest);

        var result = await service.UpdateMemoryAsync(new UpdateMemoryRequest(tenantB, created.Id, "Updated", null, "High"));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMemory_ReturnsNull_WhenMissing()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var result = await service.UpdateMemoryAsync(new UpdateMemoryRequest(Guid.NewGuid(), Guid.NewGuid(), "Updated", null, "High"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListMemory_FiltersAndPagesCorrectly()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);

        var tenantId = Guid.NewGuid();

        await Write(tenantId, "Tenant", null, "Fact", "a", "one");
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        await Write(tenantId, "User", "u1", "Summary", "b", "two");
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        await Write(tenantId, "User", "u1", "Summary", "b", "three");

        var forScope = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, "User", "u1", null, null, null, null, null, 1, 10));
        Assert.Equal(2, forScope.TotalCount);

        var kindFilter = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, null, null, "Fact", null, null, null, null, 1, 10));
        Assert.Single(kindFilter.Items);

        var dateFilter = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, null, null, null, null, null, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(5), 1, 10));
        Assert.Equal(3, dateFilter.TotalCount);

        var page1 = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, null, null, null, null, null, null, null, 1, 2));
        var page2 = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, null, null, null, null, null, null, null, 2, 2));
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page2.Items.Count);

        Assert.True(page1.Items[0].CreatedAtUtc >= page1.Items[^1].CreatedAtUtc);

        async Task<MemoryRecordResponse> Write(Guid tid, string scopeType, string? scopeId, string memoryKind, string? key, string content)
        {
            return await service.WriteMemoryAsync(new CreateMemoryRequest(tid, scopeType, scopeId, memoryKind, key, content, null, "Medium", "UserInput"));
        }
    }

    [Fact]
    public async Task StoreMemory_inserts_when_no_existing_natural_key()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        var r = await service.StoreMemoryAsync(new CreateMemoryRequest(
            tenantId, "Tenant", null, "Fact", "k1", "first", null, "Medium", "UserInput"));

        Assert.Equal("first", r.Content);
        var byKey = await service.GetMemoryByKeyAsync(new GetMemoryByKeyRequest(tenantId, "Tenant", null, "Fact", "k1"));
        Assert.NotNull(byKey);
        Assert.Equal(r.Id, byKey!.Id);
    }

    [Fact]
    public async Task StoreMemory_updates_when_natural_key_exists()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        var first = await service.StoreMemoryAsync(new CreateMemoryRequest(
            tenantId, "Tenant", null, "Fact", "same", "v1", new Dictionary<string, string> { ["a"] = "1" }, "Medium", "UserInput"));
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        var second = await service.StoreMemoryAsync(new CreateMemoryRequest(
            tenantId, "Tenant", null, "Fact", "same", "v2", new Dictionary<string, string> { ["b"] = "2" }, "High", "UserInput"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("v2", second.Content);
        Assert.Equal("High", second.Importance);
        Assert.Equal("2", second.Metadata["b"]);
        var listed = await service.ListMemoryAsync(new ListMemoryRequest(tenantId, null, null, "Fact", "same", null, null, null, 1, 10));
        Assert.Equal(1, listed.TotalCount);
    }

    [Fact]
    public async Task GetMemoryByKey_does_not_return_cross_tenant_row()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await service.StoreMemoryAsync(new CreateMemoryRequest(tenantA, "Tenant", null, "Fact", "x", "a", null, "Medium", "UserInput"));

        var wrong = await service.GetMemoryByKeyAsync(new GetMemoryByKeyRequest(tenantB, "Tenant", null, "Fact", "x"));
        Assert.Null(wrong);
    }

    [Fact]
    public async Task GetMemoryByKey_rejects_blank_key()
    {
        var repo = new InMemoryMemoryRepository();
        var service = new MemoryService(repo, new FakeDateTimeProvider { UtcNow = DateTime.UtcNow });

        await Assert.ThrowsAsync<InvalidMemoryRecordException>(() =>
            service.GetMemoryByKeyAsync(new GetMemoryByKeyRequest(Guid.NewGuid(), "Tenant", null, "Fact", "   ")));
    }

    [Fact]
    public async Task StoreMemory_without_key_inserts_each_time_like_Write()
    {
        var repo = new InMemoryMemoryRepository();
        var service = new MemoryService(repo, new FakeDateTimeProvider { UtcNow = DateTime.UtcNow });
        var tid = Guid.NewGuid();
        var a = await service.StoreMemoryAsync(new CreateMemoryRequest(tid, "Tenant", null, "Fact", null, "one", null, "Medium", "UserInput"));
        var b = await service.StoreMemoryAsync(new CreateMemoryRequest(tid, "Tenant", null, "Fact", null, "two", null, "Medium", "UserInput"));
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task ListMemory_throws_when_tenant_empty()
    {
        var repo = new InMemoryMemoryRepository();
        var service = new MemoryService(repo, new FakeDateTimeProvider { UtcNow = DateTime.UtcNow });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListMemoryAsync(new ListMemoryRequest(Guid.Empty, null, null, null, null, null, null, null, 1, 10)));
    }

    [Fact]
    public async Task RetrieveMemory_returns_top_ranked_records()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create records with different priorities
        var low = await Write(service, tenantId, "Tenant", null, "Fact", null, "low", "Low");
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        var med = await Write(service, tenantId, "User", "u1", "Summary", "key1", "med", "Medium");
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        var high = await Write(service, tenantId, "User", "u1", "Summary", "key1", "high", "High");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: "User",
            ScopeId: "u1",
            MemoryKind: "Summary",
            Key: "key1",
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(2, response.Records.Count);
        Assert.Equal(high.Id, response.Records[0].Id); // exact scope + key + higher importance
        Assert.Equal(med.Id, response.Records[1].Id);

        async Task<MemoryRecordResponse> Write(MemoryService svc, Guid tid, string scopeType, string? scopeId, string memoryKind, string? key, string content, string importance)
        {
            return await svc.WriteMemoryAsync(new CreateMemoryRequest(
                tid, scopeType, scopeId, memoryKind, key, content, null, importance, "UserInput"));
        }
    }

    // ===== PHASE 7 ALTERNATIVE: MEMORY RETRIEVAL OPTIMIZATION LAYER =====

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_PrunesLowImportanceCandidatesEarly()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create records with different importance levels
        await Write(service, tenantId, "Tenant", null, "Fact", null, "low", "Low");
        await Write(service, tenantId, "Tenant", null, "Fact", null, "medium", "Medium");
        await Write(service, tenantId, "Tenant", null, "Fact", null, "high", "High");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: "Medium", // Should prune Low importance records early
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(2, response.Records.Count);
        Assert.Contains(response.Records, r => r.Content == "medium");
        Assert.Contains(response.Records, r => r.Content == "high");
        Assert.DoesNotContain(response.Records, r => r.Content == "low");
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_PreservesRankingOrderAfterPruning()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create records that would be ranked differently without pruning
        var low = await Write(service, tenantId, "Tenant", null, "Fact", "key1", "low", "Low");
        var med = await Write(service, tenantId, "Tenant", null, "Fact", "key1", "med", "Medium");
        var high = await Write(service, tenantId, "Tenant", null, "Fact", "key1", "high", "High");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: "key1",
            Source: null,
            MinimumImportance: "Medium", // Prunes low, keeps medium and high
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(2, response.Records.Count);
        // High should come first (exact key match + higher importance)
        Assert.Equal(high.Id, response.Records[0].Id);
        Assert.Equal(med.Id, response.Records[1].Id);
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_DeduplicatesByIdDeterministically()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create a record normally
        var record = await Write(service, tenantId, "Tenant", null, "Fact", "key1", "content", "High");

        // Simulate duplicate by adding the same record twice to the repository
        // (This shouldn't happen in normal operation but tests the safety net)
        var duplicate = MemoryRecord.Create(
            record.Id,
            record.TenantId,
            MemoryScopeType.Parse("Tenant"),
            null,
            MemoryKind.Parse("Fact"),
            "key1",
            "duplicate content",
            new Dictionary<string, string>(),
            MemoryImportance.Parse("High"),
            MemorySource.Parse("UserInput"),
            record.CreatedAtUtc,
            record.UpdatedAtUtc);

        // Add duplicate directly to repo (bypassing service logic)
        await repo.AddAsync(duplicate);

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        // Should deduplicate to single record
        Assert.Single(response.Records);
        Assert.Equal(record.Id, response.Records[0].Id);
        Assert.Equal("content", response.Records[0].Content); // Original content preserved
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_DeduplicationPreservesStableOrdering()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create base record
        var baseTime = DateTime.UtcNow;
        var record1 = MemoryRecord.Create(
            Guid.NewGuid(),
            tenantId,
            MemoryScopeType.Parse("Tenant"),
            null,
            MemoryKind.Parse("Fact"),
            "key1",
            "content1",
            new Dictionary<string, string>(),
            MemoryImportance.Parse("High"),
            MemorySource.Parse("UserInput"),
            baseTime,
            baseTime);

        var record2 = MemoryRecord.Create(
            record1.Id, // Same ID
            tenantId,
            MemoryScopeType.Parse("Tenant"),
            null,
            MemoryKind.Parse("Fact"),
            "key1",
            "content2", // Different content
            new Dictionary<string, string>(),
            MemoryImportance.Parse("High"),
            MemorySource.Parse("UserInput"),
            baseTime,
            baseTime.AddSeconds(1)); // Newer

        // Add both (simulating duplicates)
        await repo.AddAsync(record1);
        await repo.AddAsync(record2);

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        // Should deduplicate to single record
        Assert.Single(response.Records);
        // Deduplication should preserve the highest-ranked duplicate (newer CreatedAtUtc wins)
        Assert.Equal(record2.Content, response.Records[0].Content);
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_MaxResultsRespectedAfterOptimization()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create many records
        for (int i = 0; i < 10; i++)
        {
            await Write(service, tenantId, "Tenant", null, "Fact", null, $"content{i}", "High");
        }

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 3, // Limit to 3
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(3, response.Records.Count);
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_NoCrossTenantLeakage()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await Write(service, tenantA, "Tenant", null, "Fact", null, "tenantA", "High");
        await Write(service, tenantB, "Tenant", null, "Fact", null, "tenantB", "High");

        var responseA = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantA,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        var responseB = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantB,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Single(responseA.Records);
        Assert.Single(responseB.Records);
        Assert.Equal("tenantA", responseA.Records[0].Content);
        Assert.Equal("tenantB", responseB.Records[0].Content);
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_RankingDeterministicAfterOptimization()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create records with identical ranking criteria except Id
        var record1 = await Write(service, tenantId, "Tenant", null, "Fact", null, "content1", "High");
        var record2 = await Write(service, tenantId, "Tenant", null, "Fact", null, "content2", "High");

        // Run multiple times to ensure deterministic ordering
        var response1 = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        var response2 = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        // Results should be identical and deterministic
        Assert.Equal(2, response1.Records.Count);
        Assert.Equal(2, response2.Records.Count);
        Assert.Equal(response1.Records[0].Id, response2.Records[0].Id);
        Assert.Equal(response1.Records[1].Id, response2.Records[1].Id);
    }

    [Fact]
    public async Task RetrieveMemory_optimizationLayer_PruningDoesNotRemoveEligibleRecords()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        // Create records with different importance levels
        var low = await Write(service, tenantId, "Tenant", null, "Fact", null, "low", "Low");
        var med = await Write(service, tenantId, "Tenant", null, "Fact", null, "med", "Medium");
        var high = await Write(service, tenantId, "Tenant", null, "Fact", null, "high", "High");

        // Request with no minimum importance (should include all)
        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(3, response.Records.Count);
        Assert.Contains(response.Records, r => r.Content == "low");
        Assert.Contains(response.Records, r => r.Content == "med");
        Assert.Contains(response.Records, r => r.Content == "high");
    }

    [Fact]
    public async Task RetrieveMemory_filters_by_minimum_importance()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        await Write(service, tenantId, "Tenant", null, "Fact", null, "low", "Low");
        await Write(service, tenantId, "Tenant", null, "Fact", null, "high", "High");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: "Medium",
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Single(response.Records);
        Assert.Equal("High", response.Records[0].Importance);
    }

    [Fact]
    public async Task RetrieveMemory_enforces_max_results()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
            await Write(service, tenantId, "Tenant", null, "Fact", null, $"content{i}", "Medium");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 3,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(3, response.Records.Count);
    }

    [Fact]
    public async Task RetrieveMemory_does_not_return_cross_tenant()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await Write(service, tenantA, "Tenant", null, "Fact", null, "a", "Medium");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantB,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: null,
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Empty(response.Records);
    }

    [Fact]
    public async Task RetrieveMemory_ranks_exact_key_higher()
    {
        var repo = new InMemoryMemoryRepository();
        var provider = new FakeDateTimeProvider { UtcNow = DateTime.UtcNow };
        var service = new MemoryService(repo, provider);
        var tenantId = Guid.NewGuid();

        var noKey = await Write(service, tenantId, "Tenant", null, "Fact", null, "no key", "High");
        provider.UtcNow = provider.UtcNow.AddMinutes(1);
        var withKey = await Write(service, tenantId, "Tenant", null, "Fact", "specific", "with key", "Medium");

        var response = await service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
            TenantId: tenantId,
            ScopeType: null,
            ScopeId: null,
            MemoryKind: null,
            Key: "specific",
            Source: null,
            MinimumImportance: null,
            MaxResults: 10,
            CreatedAfterUtc: null,
            CreatedBeforeUtc: null));

        Assert.Equal(1, response.Records.Count);
        Assert.Equal(withKey.Id, response.Records[0].Id); // exact key match first
    }

    [Fact]
    public async Task RetrieveMemory_throws_on_invalid_max_results()
    {
        var repo = new InMemoryMemoryRepository();
        var service = new MemoryService(repo, new FakeDateTimeProvider { UtcNow = DateTime.UtcNow });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
                TenantId: Guid.NewGuid(),
                ScopeType: null,
                ScopeId: null,
                MemoryKind: null,
                Key: null,
                Source: null,
                MinimumImportance: null,
                MaxResults: 0,
                CreatedAfterUtc: null,
                CreatedBeforeUtc: null)));
    }

    [Fact]
    public async Task RetrieveMemory_throws_on_empty_tenant()
    {
        var repo = new InMemoryMemoryRepository();
        var service = new MemoryService(repo, new FakeDateTimeProvider { UtcNow = DateTime.UtcNow });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RetrieveMemoryAsync(new RetrieveMemoryRequest(
                TenantId: Guid.Empty,
                ScopeType: null,
                ScopeId: null,
                MemoryKind: null,
                Key: null,
                Source: null,
                MinimumImportance: null,
                MaxResults: 10,
                CreatedAfterUtc: null,
                CreatedBeforeUtc: null)));
    }

    private static async Task<MemoryRecordResponse> Write(MemoryService service, Guid tenantId, string scopeType, string? scopeId, string memoryKind, string? key, string content, string importance)
    {
        return await service.WriteMemoryAsync(new CreateMemoryRequest(
            tenantId, scopeType, scopeId, memoryKind, key, content, null, importance, "UserInput"));
    }
}
