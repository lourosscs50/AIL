using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record ExecuteIntelligenceRequest(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    Dictionary<string, string> Variables,
    List<string> ContextReferenceIds,
    bool IncludeMemory = false,
    ExecuteIntelligenceMemoryRequest? MemoryQuery = null,
    Guid? ExecutionInstanceId = null,
    string? TraceThreadId = null,
    Guid? CorrelationGroupId = null);
