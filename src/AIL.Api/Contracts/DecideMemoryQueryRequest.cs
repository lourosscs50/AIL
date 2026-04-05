using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record DecideMemoryQueryRequest(
    string ScopeType,
    string? ScopeId,
    string? MemoryKind,
    IReadOnlyList<string>? Keys = null,
    int? TakeRecent = null,
    bool IncludeMetadata = true);
