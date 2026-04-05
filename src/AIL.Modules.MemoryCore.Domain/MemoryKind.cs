using System;
using System.Collections.Generic;
using System.Linq;

namespace AIL.Modules.MemoryCore.Domain;

public sealed record MemoryKind(string Value)
{
    public static MemoryKind Fact => new("Fact");
    public static MemoryKind Summary => new("Summary");

    private static readonly IReadOnlyDictionary<string, MemoryKind> Map = new Dictionary<string, MemoryKind>(StringComparer.OrdinalIgnoreCase)
    {
        [Fact.Value] = Fact,
        [Summary.Value] = Summary,
    };

    public static MemoryKind Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidMemoryRecordException("MemoryKind is required.");

        if (Map.TryGetValue(value.Trim(), out var kind))
            return kind;

        throw new InvalidMemoryRecordException($"Unsupported MemoryKind '{value}'.");
    }

    public override string ToString() => Value;
}
