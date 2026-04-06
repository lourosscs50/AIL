using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

/// <summary>
/// Stable, platform-generic request body for the A.I.L. Decision capability (<c>POST /decisions</c>).
/// Carries only bounded operator context and decision inputs—never raw model payloads or prompts.
/// Size limits are enforced at the API boundary by <see cref="AIL.Api.DecisionEndpointMapping"/>.
/// Optional <c>ExecutionInstanceId</c> is an external execution reference only; A.I.L. never generates or infers it.
/// </summary>
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
    IReadOnlyDictionary<string, string>? Metadata = null,
    Guid? CorrelationGroupId = null,
    Guid? ExecutionInstanceId = null);
