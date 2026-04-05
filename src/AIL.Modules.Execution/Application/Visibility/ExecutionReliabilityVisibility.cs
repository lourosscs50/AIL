namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>Provider / fallback selection snapshot (observability only).</summary>
public sealed record ExecutionReliabilityVisibility(
    bool FallbackUsed,
    string? PolicyKey,
    string? StrategyKey,
    string PrimaryProviderKey,
    string PrimaryModelKey,
    string? SelectedProviderKey,
    string? SelectedModelKey,
    string? FallbackProviderKey,
    string? FallbackModelKey);
