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
    string? ErrorMessage = null,
    bool MemoryRequested = false,
    int? MemoryItemCount = null,
    string? TraceThreadId = null,
    Guid? CorrelationGroupId = null,
    Guid? ExecutionInstanceId = null);
