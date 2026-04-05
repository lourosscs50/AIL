using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Domain;
using AIL.Modules.MemoryCore.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AIL.Modules.MemoryCore.Infrastructure.Tests;

public sealed class FileMemoryRepositoryTests
{
    private static string NewTempJsonPath() =>
        Path.Combine(Path.GetTempPath(), $"ail-memcore-test-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_same_row_for_tenant()
    {
        var path = NewTempJsonPath();
        try
        {
            var tenant = Guid.NewGuid();
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var record = MemoryRecord.Create(
                id,
                tenant,
                MemoryScopeType.Tenant,
                null,
                MemoryKind.Fact,
                "k1",
                "payload",
                null,
                MemoryImportance.Medium,
                MemorySource.UserInput,
                now,
                now);

            var repo = new FileMemoryRepository(path);
            await repo.AddAsync(record);

            var loaded = await repo.GetByIdAsync(tenant, id);
            Assert.NotNull(loaded);
            Assert.Equal("payload", loaded!.Content);
            Assert.Equal(MemoryKind.Fact, loaded.MemoryKind);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task New_repository_loads_records_persisted_to_file()
    {
        var path = NewTempJsonPath();
        try
        {
            var tenant = Guid.NewGuid();
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var record = MemoryRecord.Create(
                id,
                tenant,
                MemoryScopeType.Tenant,
                null,
                MemoryKind.Fact,
                "persist-k",
                "round-trip",
                null,
                MemoryImportance.Low,
                MemorySource.SystemDerived,
                now,
                now);

            await new FileMemoryRepository(path).AddAsync(record);

            var reloaded = new FileMemoryRepository(path);
            var loaded = await reloaded.GetByIdAsync(tenant, id);
            Assert.NotNull(loaded);
            Assert.Equal("round-trip", loaded!.Content);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_tenant_mismatch()
    {
        var path = NewTempJsonPath();
        try
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var record = MemoryRecord.Create(
                id,
                tenantA,
                MemoryScopeType.Tenant,
                null,
                MemoryKind.Fact,
                "k",
                "a-only",
                null,
                MemoryImportance.Medium,
                MemorySource.UserInput,
                now,
                now);

            await new FileMemoryRepository(path).AddAsync(record);

            var repo = new FileMemoryRepository(path);
            Assert.Null(await repo.GetByIdAsync(tenantB, id));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task AddAsync_throws_when_id_already_exists()
    {
        var path = NewTempJsonPath();
        try
        {
            var tenant = Guid.NewGuid();
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var record = MemoryRecord.Create(
                id,
                tenant,
                MemoryScopeType.Tenant,
                null,
                MemoryKind.Fact,
                "k",
                "one",
                null,
                MemoryImportance.Medium,
                MemorySource.UserInput,
                now,
                now);

            var repo = new FileMemoryRepository(path);
            await repo.AddAsync(record);

            var duplicate = MemoryRecord.Create(
                id,
                tenant,
                MemoryScopeType.Tenant,
                null,
                MemoryKind.Fact,
                "k2",
                "two",
                null,
                MemoryImportance.Medium,
                MemorySource.UserInput,
                now.AddMinutes(1),
                now.AddMinutes(1));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddAsync(duplicate));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task GetByKeyAsync_finds_row_by_natural_key_after_persist()
    {
        var path = NewTempJsonPath();
        try
        {
            var tenant = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var record = MemoryRecord.Create(
                Guid.NewGuid(),
                tenant,
                MemoryScopeType.User,
                "user-42",
                MemoryKind.Summary,
                "lookup-key",
                "by-key-body",
                null,
                MemoryImportance.High,
                MemorySource.UserInput,
                now,
                now);

            await new FileMemoryRepository(path).AddAsync(record);

            var repo = new FileMemoryRepository(path);
            var nk = new MemoryNaturalKey(tenant, MemoryScopeType.User, "user-42", MemoryKind.Summary, "lookup-key");
            var found = await repo.GetByKeyAsync(nk);

            Assert.NotNull(found);
            Assert.Equal("by-key-body", found!.Content);
        }
        finally
        {
            TryDelete(path);
        }
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
            // best-effort cleanup for temp test files
        }
    }
}
