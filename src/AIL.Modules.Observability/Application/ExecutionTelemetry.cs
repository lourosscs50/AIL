using System;

namespace AIL.Modules.Observability.Application;

public sealed record ExecutionTelemetry(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    string? PromptVersion,
    string? PolicyKey,
    string ProviderKey,
    string ModelKey,
    bool UsedFallback,
    int? InputTokenCount,
    int? OutputTokenCount,
    long DurationMs,
    bool Succeeded,
    string? ErrorMessage = null);
