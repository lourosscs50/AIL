using System;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record GetMemoryByKeyRequest(
    Guid TenantId,
    string ScopeType,
    string? ScopeId,
    string MemoryKind,
    string Key);
