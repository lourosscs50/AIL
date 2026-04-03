using System;
using System.Collections.Generic;

namespace AIL.Modules.Audit.Domain;

public sealed record AuditRecord(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    string? PromptVersion,
    string? PolicyKey,
    string? ProviderKey,
    string? ModelKey,
    bool UsedFallback,
    int? InputTokenCount,
    int? OutputTokenCount,
    IReadOnlyList<string> ContextReferenceIds,
    DateTime ExecutedAtUtc,
    long DurationMs,
    string Outcome,
    string? Notes);
