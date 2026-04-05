using System;
using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

public sealed record MemoryContextItem(
    string? Key,
    string Content,
    string MemoryKind,
    string ScopeType,
    string? ScopeId,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTime UpdatedAtUtc);
