using System.Collections.Generic;

namespace AIL.Api.Contracts;

/// <summary>
/// Bounded memory inclusion parameters for a decision request. Used only when <c>IncludeMemory</c> is true.
/// </summary>
public sealed record DecideMemoryQueryRequest(
    string ScopeType,
    string? ScopeId,
    string? MemoryKind,
    IReadOnlyList<string>? Keys = null,
    int? TakeRecent = null,
    bool IncludeMetadata = true);
