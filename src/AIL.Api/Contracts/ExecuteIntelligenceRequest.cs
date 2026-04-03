using System;
using System.Collections.Generic;

namespace AIL.Api.Contracts;

public sealed record ExecuteIntelligenceRequest(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    Dictionary<string, string> Variables,
    List<string> ContextReferenceIds);
