namespace AIL.Modules.PolicyRegistry.Domain;

/// <summary>
/// Defines execution policy metadata and provider selection preferences for a capability.
/// </summary>
public sealed record ExecutionPolicy(
    string PolicyKey,
    string Description,
    string PrimaryProviderKey,
    string PrimaryModelKey,
    bool FallbackAllowed,
    string? FallbackProviderKey,
    string? FallbackModelKey,
    int MaxTokens,
    int TimeoutMs);
