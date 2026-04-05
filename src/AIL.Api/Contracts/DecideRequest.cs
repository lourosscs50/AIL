using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record DecideRequest(
    Guid TenantId,
    string DecisionType,
    string SubjectType,
    string SubjectId,
    string? ContextText,
    IReadOnlyDictionary<string, string>? StructuredContext,
    bool IncludeMemory = false,
    DecideMemoryQueryRequest? MemoryQuery = null,
    IReadOnlyList<string>? CandidateStrategies = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
