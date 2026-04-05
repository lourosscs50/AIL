using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record ExecuteIntelligenceMemoryRequest(
    string ScopeType,
    string? ScopeId,
    string? MemoryKind,
    List<string>? Keys = null,
    int? TakeRecent = null,
    bool IncludeMetadata = true);
