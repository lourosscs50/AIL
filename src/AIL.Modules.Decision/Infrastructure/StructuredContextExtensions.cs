using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Infrastructure;

internal static class StructuredContextExtensions
{
    public static bool TryGetValueOrdinalIgnoreCase(
        this IReadOnlyDictionary<string, string> inputs,
        string key,
        out string? value)
    {
        foreach (var kv in inputs)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public static bool IsEscalated(this IReadOnlyDictionary<string, string> inputs)
    {
        if (inputs.TryGetValueOrdinalIgnoreCase("escalation", out var e) &&
            string.Equals(e?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (inputs.TryGetValueOrdinalIgnoreCase("priority", out var p) &&
            string.Equals(p?.Trim(), "high", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool IsContextSensitive(this IReadOnlyDictionary<string, string> inputs) =>
        inputs.TryGetValueOrdinalIgnoreCase("context_sensitive", out var v) &&
        string.Equals(v?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
