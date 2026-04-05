using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record RetrieveMemoryResponse(
    IReadOnlyList<MemoryRecordResponse> Records);

public sealed record MemoryContext(
    IReadOnlyList<MemoryContextItem> Items);

public sealed record MemoryContextItem(
    string? Key,
    string Content,
    string Importance,
    string Source,
    string? ScopeType,
    string? ScopeId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);