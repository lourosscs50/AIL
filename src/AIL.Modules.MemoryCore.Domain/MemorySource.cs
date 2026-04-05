using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Domain;

public sealed record MemorySource(string Value)
{
    public static MemorySource UserInput => new("UserInput");
    public static MemorySource SystemDerived => new("SystemDerived");
    public static MemorySource ExecutionOutput => new("ExecutionOutput");
    public static MemorySource Imported => new("Imported");

    private static readonly IReadOnlyDictionary<string, MemorySource> Map = new Dictionary<string, MemorySource>(StringComparer.OrdinalIgnoreCase)
    {
        [UserInput.Value] = UserInput,
        [SystemDerived.Value] = SystemDerived,
        [ExecutionOutput.Value] = ExecutionOutput,
        [Imported.Value] = Imported,
    };

    public static MemorySource Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidMemoryRecordException("Source is required.");

        if (Map.TryGetValue(value.Trim(), out var source))
            return source;

        throw new InvalidMemoryRecordException($"Unsupported Source '{value}'.");
    }

    public override string ToString() => Value;
}
