using System;
using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

/// <summary>
/// Execution input. <see cref="ExecutionInstanceId"/> is optional; <see cref="TraceThreadId"/> is the cross-system trace thread (not the execution id); <see cref="CorrelationGroupId"/> is optional broader grouping.
/// </summary>
public sealed record ExecutionRequest(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    Dictionary<string, string> Variables,
    List<string> ContextReferenceIds,
    bool? IncludeMemory = null,
    ExecutionMemoryQuery? MemoryQuery = null,
    int? MemoryMaxResults = null,
    Guid? ExecutionInstanceId = null,
    string? TraceThreadId = null,
    Guid? CorrelationGroupId = null,
    /// <summary>Optional ChronoFlow control execution row id when the caller links this invocation to a concrete execution record (read-only observability).</summary>
    Guid? ChronoFlowExecutionInstanceId = null);
