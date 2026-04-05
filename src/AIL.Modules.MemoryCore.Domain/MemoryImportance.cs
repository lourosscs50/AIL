using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Domain;

public sealed record MemoryImportance(string Value)
{
    public static MemoryImportance Low => new("Low");
    public static MemoryImportance Medium => new("Medium");
    public static MemoryImportance High => new("High");
    public static MemoryImportance Critical => new("Critical");

    private static readonly IReadOnlyDictionary<string, MemoryImportance> Map = new Dictionary<string, MemoryImportance>(StringComparer.OrdinalIgnoreCase)
    {
        [Low.Value] = Low,
        [Medium.Value] = Medium,
        [High.Value] = High,
        [Critical.Value] = Critical,
    };

    public static MemoryImportance Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidMemoryRecordException("Importance is required.");

        if (Map.TryGetValue(value.Trim(), out var importance))
            return importance;

        throw new InvalidMemoryRecordException($"Unsupported Importance '{value}'.");
    }

    public override string ToString() => Value;
}
