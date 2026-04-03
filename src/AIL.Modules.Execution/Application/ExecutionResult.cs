using System;

namespace AIL.Modules.Execution.Application;

public sealed record ExecutionResult(
    bool IsAllowed,
    string? DenyReason,
    string OutputText,
    string ProviderKey,
    string ModelKey,
    string PromptVersion,
    Guid AuditRecordId);
