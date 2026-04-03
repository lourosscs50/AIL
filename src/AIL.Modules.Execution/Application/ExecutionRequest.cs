using System;
using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

public sealed record ExecutionRequest(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    Dictionary<string, string> Variables,
    List<string> ContextReferenceIds);
