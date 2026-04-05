using System;
using AIL.Modules.Execution.Application.Visibility;

namespace AIL.Modules.Execution.Application;

public sealed record ExecutionResult(
    bool IsAllowed,
    string? DenyReason,
    string OutputText,
    string ProviderKey,
    string ModelKey,
    string PromptVersion,
    Guid AuditRecordId,
    Guid ExecutionInstanceId,
    ExecutionVisibilityReadModel Visibility);
