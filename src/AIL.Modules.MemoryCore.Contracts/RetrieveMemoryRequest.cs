using System;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record RetrieveMemoryRequest(
    Guid TenantId,
    string? ScopeType,
    string? ScopeId,
    string? MemoryKind,
    string? Key,
    string? Source,
    string? MinimumImportance,
    int MaxResults,
    DateTime? CreatedAfterUtc,
    DateTime? CreatedBeforeUtc);