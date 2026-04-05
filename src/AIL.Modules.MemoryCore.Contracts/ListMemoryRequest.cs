using System;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record ListMemoryRequest(
    Guid TenantId,
    string? ScopeType,
    string? ScopeId,
    string? MemoryKind,
    string? Key,
    string? Source,
    DateTime? FromCreatedAtUtc,
    DateTime? ToCreatedAtUtc,
    int PageNumber,
    int PageSize);
