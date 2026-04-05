using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record CreateMemoryRequest(
    Guid TenantId,
    string ScopeType,
    string? ScopeId,
    string MemoryKind,
    string? Key,
    string Content,
    IReadOnlyDictionary<string, string>? Metadata,
    string Importance,
    string Source);
