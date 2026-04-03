using System;

namespace AIL.Api.Contracts;

public sealed record ExecuteIntelligenceResponse(
    string OutputText,
    string ProviderKey,
    string ModelKey,
    string PromptVersion,
    Guid AuditRecordId);
