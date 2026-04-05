using AIL.Modules.MemoryCore.Domain;
using System;

namespace AIL.Modules.MemoryCore.Application;

public sealed record MemoryListFilter(
    Guid TenantId,
    MemoryScopeType? ScopeType,
    string? ScopeId,
    MemoryKind? MemoryKind,
    string? Key,
    MemorySource? Source,
    DateTime? FromCreatedAtUtc,
    DateTime? ToCreatedAtUtc,
    int PageNumber,
    int PageSize);