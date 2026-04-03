using System;

namespace AIL.Modules.Execution.Application;

public sealed record ProviderExecutionResult(
    string ProviderKey,
    string ModelKey,
    string OutputText,
    bool UsedFallback,
    int? InputTokenCount,
    int? OutputTokenCount);
