using System;
using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

/// <summary>
/// Describes what provider/model should be used for execution including optional fallback.
/// </summary>
public sealed record ProviderExecutionRequest(
    Guid TenantId,
    string CapabilityKey,
    string PromptKey,
    string PromptVersion,
    string PromptText,
    string ContextText,
    int MaxTokens,
    bool FallbackAllowed,
    string PrimaryProviderKey,
    string PrimaryModelKey,
    string? FallbackProviderKey,
    string? FallbackModelKey,
    IReadOnlyDictionary<string, string> Metadata);
