namespace AIL.Modules.ProviderRegistry.Domain;

/// <summary>
/// Describes a provider and its supported default model(s).
/// </summary>
public sealed record ProviderDefinition(
    string ProviderKey,
    string DefaultModelKey,
    string[] SupportedModelKeys,
    bool IsEnabled);