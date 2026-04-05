using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>Deterministic memory retrieval inputs (tenant comes from <see cref="DecisionRequest.TenantId"/>).</summary>
public sealed record DecisionMemoryQuery(
    string ScopeType,
    string? ScopeId,
    string? MemoryKind,
    IReadOnlyList<string>? Keys = null,
    int? TakeRecent = null,
    bool IncludeMetadata = true);
