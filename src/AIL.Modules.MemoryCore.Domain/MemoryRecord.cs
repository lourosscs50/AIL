using System;
using System.Collections.Generic;
using System.Linq;

namespace AIL.Modules.MemoryCore.Domain;

/// <summary>
/// Immutable memory row. Valid instances are produced only through <see cref="Create"/> (or <c>with</c> from an existing valid row).
/// Construction is funneled through a private constructor so every instance has fully assigned invariants after validation in <see cref="Create"/>.
/// </summary>
public sealed record MemoryRecord
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public MemoryScopeType ScopeType { get; init; }
    public string? ScopeId { get; init; }
    public MemoryKind MemoryKind { get; init; }
    public string? Key { get; init; }
    public string Content { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
    public MemoryImportance Importance { get; init; }
    public MemorySource Source { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    private MemoryRecord(
        Guid id,
        Guid tenantId,
        MemoryScopeType scopeType,
        string? scopeId,
        MemoryKind memoryKind,
        string? key,
        string content,
        IReadOnlyDictionary<string, string> metadata,
        MemoryImportance importance,
        MemorySource source,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        MemoryKind = memoryKind;
        Key = key;
        Content = content;
        Metadata = metadata;
        Importance = importance;
        Source = source;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static MemoryRecord Create(
        Guid id,
        Guid tenantId,
        MemoryScopeType scopeType,
        string? scopeId,
        MemoryKind memoryKind,
        string? key,
        string content,
        IReadOnlyDictionary<string, string>? metadata,
        MemoryImportance importance,
        MemorySource source,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        if (id == Guid.Empty)
            throw new InvalidMemoryRecordException("Id is required.");

        if (tenantId == Guid.Empty)
            throw new InvalidMemoryRecordException("TenantId is required.");

        if (scopeType is null)
            throw new InvalidMemoryRecordException("ScopeType is required.");

        if (memoryKind is null)
            throw new InvalidMemoryRecordException("MemoryKind is required.");

        if (importance is null)
            throw new InvalidMemoryRecordException("Importance is required.");

        if (source is null)
            throw new InvalidMemoryRecordException("Source is required.");

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidMemoryRecordException("Content is required.");

        if (!scopeType.IsTenant)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new InvalidMemoryRecordException("ScopeId is required for non-tenant ScopeType.");
        }

        var normalizedKey = NormalizeKey(key);

        var validatedMetadata = ValidateMetadata(metadata);

        createdAtUtc = EnsureUtcTime(createdAtUtc, nameof(createdAtUtc));
        updatedAtUtc = EnsureUtcTime(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < createdAtUtc)
            throw new InvalidMemoryRecordException("UpdatedAtUtc cannot be before CreatedAtUtc.");

        return new MemoryRecord(
            id,
            tenantId,
            scopeType,
            scopeId,
            memoryKind,
            normalizedKey,
            content.Trim(),
            validatedMetadata,
            importance,
            source,
            createdAtUtc,
            updatedAtUtc);
    }

    public static string? NormalizeKey(string? key)
    {
        if (key is null)
            return null;

        var trimmed = key.Trim();

        if (trimmed.Length == 0)
            throw new InvalidMemoryRecordException("Key cannot be blank.");

        return trimmed.ToLowerInvariant();
    }

    internal static IReadOnlyDictionary<string, string> ValidateMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
            return new Dictionary<string, string>();

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in metadata)
        {
            if (kvp.Key is null)
                throw new InvalidMemoryRecordException("Metadata key cannot be null.");
            if (string.IsNullOrWhiteSpace(kvp.Key))
                throw new InvalidMemoryRecordException("Metadata key cannot be empty.");
            if (kvp.Value is null)
                throw new InvalidMemoryRecordException($"Metadata value for key '{kvp.Key}' cannot be null.");

            dictionary[kvp.Key] = kvp.Value;
        }

        return dictionary;
    }

    internal static DateTime EnsureUtcTime(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new InvalidMemoryRecordException($"{name} must be UTC.");

        return value;
    }

    public MemoryRecord WithUpdatedContent(string updatedContent, DateTime updatedAtUtc)
    {
        return WithUpdatedState(
            updatedContent: updatedContent,
            metadata: Metadata,
            importance: Importance,
            updatedAtUtc: updatedAtUtc);
    }

    public MemoryRecord WithUpdatedState(
        string updatedContent,
        IReadOnlyDictionary<string, string>? metadata,
        MemoryImportance importance,
        DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(updatedContent))
            throw new InvalidMemoryRecordException("Content is required.");

        if (importance is null)
            throw new InvalidMemoryRecordException("Importance is required.");

        var validatedMetadata = ValidateMetadata(metadata);

        updatedAtUtc = EnsureUtcTime(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc <= UpdatedAtUtc)
            throw new InvalidMemoryRecordException("UpdatedAtUtc must be later than current UpdatedAtUtc.");

        return this with
        {
            Content = updatedContent.Trim(),
            Metadata = validatedMetadata,
            Importance = importance,
            UpdatedAtUtc = updatedAtUtc
        };
    }
}
