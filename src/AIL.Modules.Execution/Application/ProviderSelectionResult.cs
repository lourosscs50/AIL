namespace AIL.Modules.Execution.Application;

/// <summary>
/// The result of selecting a provider/model (primary + optional fallback) for a given capability.
/// </summary>
public sealed record ProviderSelectionResult(
    string PrimaryProviderKey,
    string PrimaryModelKey,
    string? FallbackProviderKey,
    string? FallbackModelKey,
    bool FallbackAllowed,
    int MaxTokens,
    int TimeoutMs);
