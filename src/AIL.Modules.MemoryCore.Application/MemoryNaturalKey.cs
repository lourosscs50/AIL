using System;
using AIL.Modules.MemoryCore.Domain;

namespace AIL.Modules.MemoryCore.Application;

/// <summary>Deterministic natural key for tenant-scoped memory lookup (upsert / get-by-key).</summary>
public sealed record MemoryNaturalKey
{
    public Guid TenantId { get; }
    public MemoryScopeType ScopeType { get; }
    public string? ScopeId { get; }
    public MemoryKind MemoryKind { get; }
    public string Key { get; }

    public MemoryNaturalKey(Guid tenantId, MemoryScopeType scopeType, string? scopeId, MemoryKind memoryKind, string key)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (scopeType is null)
            throw new ArgumentNullException(nameof(scopeType));

        if (memoryKind is null)
            throw new ArgumentNullException(nameof(memoryKind));

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidMemoryRecordException("Key is required for natural-key operations.");

        TenantId = tenantId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        MemoryKind = memoryKind;
        Key = MemoryRecord.NormalizeKey(key) ?? throw new InvalidMemoryRecordException("Key is required.");
    }

    /// <summary>Normalized scope id for persistence and equality (empty string when absent).</summary>
    public string ScopeIdForPersistence =>
        string.IsNullOrWhiteSpace(ScopeId) ? "" : ScopeId.Trim();
}
