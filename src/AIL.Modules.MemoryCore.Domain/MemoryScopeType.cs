using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Domain;

public sealed record MemoryScopeType(string Value)
{
    public static MemoryScopeType Tenant => new("Tenant");
    public static MemoryScopeType User => new("User");
    public static MemoryScopeType Session => new("Session");
    public static MemoryScopeType Workflow => new("Workflow");
    public static MemoryScopeType System => new("System");

    private static readonly IReadOnlyDictionary<string, MemoryScopeType> Map = new Dictionary<string, MemoryScopeType>(StringComparer.OrdinalIgnoreCase)
    {
        [Tenant.Value] = Tenant,
        [User.Value] = User,
        [Session.Value] = Session,
        [Workflow.Value] = Workflow,
        [System.Value] = System,
    };

    public static MemoryScopeType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidMemoryRecordException("ScopeType is required.");

        if (Map.TryGetValue(value.Trim(), out var scopeType))
            return scopeType;

        throw new InvalidMemoryRecordException($"Unsupported ScopeType '{value}'.");
    }

    public bool IsTenant => string.Equals(Value, Tenant.Value, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Value;
}
