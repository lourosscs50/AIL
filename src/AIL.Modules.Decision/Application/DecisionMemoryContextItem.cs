using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionMemoryContextItem(
    string? Key,
    string Content,
    string MemoryKind,
    string ScopeType,
    string? ScopeId,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTime UpdatedAtUtc);
