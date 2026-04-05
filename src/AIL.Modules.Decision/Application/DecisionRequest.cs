using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionRequest(
    Guid TenantId,
    string DecisionType,
    string SubjectType,
    string SubjectId,
    string? ContextText,
    IReadOnlyDictionary<string, string>? StructuredContext,
    bool IncludeMemory = false,
    DecisionMemoryQuery? MemoryQuery = null,
    IReadOnlyList<string>? CandidateStrategies = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
