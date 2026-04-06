using System;
using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Application-layer decision input for <see cref="IDecisionService"/>; hosts map their transport contracts to this shape.
/// </summary>
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
    IReadOnlyDictionary<string, string>? Metadata = null,
    Guid? CorrelationGroupId = null);
