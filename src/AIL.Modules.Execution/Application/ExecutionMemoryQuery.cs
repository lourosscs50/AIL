using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

/// <summary>
/// Deterministic, non-semantic memory retrieval inputs for execution (tenant comes from <see cref="ExecutionRequest.TenantId"/>).
/// </summary>
public sealed record ExecutionMemoryQuery(
    string ScopeType,
    string? ScopeId,
    string? MemoryKind,
    IReadOnlyList<string>? Keys = null,
    int? TakeRecent = null,
    bool IncludeMetadata = true);
