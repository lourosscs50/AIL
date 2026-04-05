using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using Xunit;

namespace AIL.Modules.MemoryCore.Domain.Tests;

public class MemoryRecordLifecycleTests
{
    [Fact]
    public void UpdatePreservesImmutableFields()
    {
        var now = DateTime.UtcNow;
        var record = MemoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MemoryScopeType.User,
            "user-1",
            MemoryKind.Fact,
            "key",
            "content",
            new Dictionary<string, string> { ["a"] = "1" },
            MemoryImportance.Medium,
            MemorySource.UserInput,
            now,
            now);

        var updated = record.WithUpdatedState("new content", new Dictionary<string, string> { ["b"] = "2" }, MemoryImportance.High, now.AddMinutes(1));

        Assert.Equal(record.Id, updated.Id);
        Assert.Equal(record.TenantId, updated.TenantId);
        Assert.Equal(record.ScopeType, updated.ScopeType);
        Assert.Equal(record.ScopeId, updated.ScopeId);
        Assert.Equal(record.MemoryKind, updated.MemoryKind);
        Assert.Equal(record.Source, updated.Source);
        Assert.Equal(record.CreatedAtUtc, updated.CreatedAtUtc);

        Assert.Equal("new content", updated.Content);
        Assert.Equal("2", updated.Metadata["b"]);
        Assert.Equal(MemoryImportance.High, updated.Importance);
        Assert.Equal(now.AddMinutes(1), updated.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateRejectsInvalidContent()
    {
        var now = DateTime.UtcNow;
        var record = MemoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MemoryScopeType.User,
            "user-1",
            MemoryKind.Fact,
            "key",
            "content",
            new Dictionary<string, string> { ["a"] = "1" },
            MemoryImportance.Medium,
            MemorySource.UserInput,
            now,
            now);

        Assert.Throws<InvalidMemoryRecordException>(() => record.WithUpdatedState("   ", record.Metadata, MemoryImportance.Medium, now.AddMinutes(1)));
    }

    [Fact]
    public void UpdateRejectsInvalidMetadata()
    {
        var now = DateTime.UtcNow;
        var record = MemoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MemoryScopeType.User,
            "user-1",
            MemoryKind.Fact,
            "key",
            "content",
            new Dictionary<string, string> { ["a"] = "1" },
            MemoryImportance.Medium,
            MemorySource.UserInput,
            now,
            now);

        var badMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["   "] = "value"
        };

        Assert.Throws<InvalidMemoryRecordException>(() => record.WithUpdatedState("new content", badMetadata, MemoryImportance.Medium, now.AddMinutes(1)));
    }

    [Fact]
    public void UpdateRejectsInvalidImportance()
    {
        var now = DateTime.UtcNow;
        var record = MemoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MemoryScopeType.User,
            "user-1",
            MemoryKind.Fact,
            "key",
            "content",
            new Dictionary<string, string> { ["a"] = "1" },
            MemoryImportance.Medium,
            MemorySource.UserInput,
            now,
            now);

        Assert.Throws<InvalidMemoryRecordException>(() => record.WithUpdatedState("new content", record.Metadata, null!, now.AddMinutes(1)));
    }

    [Fact]
    public void UpdateEnforcesUpdatedAtUtcMonotonicity()
    {
        var now = DateTime.UtcNow;
        var record = MemoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MemoryScopeType.User,
            "user-1",
            MemoryKind.Fact,
            "key",
            "content",
            new Dictionary<string, string> { ["a"] = "1" },
            MemoryImportance.Medium,
            MemorySource.UserInput,
            now,
            now);

        Assert.Throws<InvalidMemoryRecordException>(() => record.WithUpdatedState("new content", record.Metadata, MemoryImportance.High, now));
    }
}
