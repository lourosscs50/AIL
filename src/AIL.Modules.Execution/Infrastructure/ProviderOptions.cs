namespace AIL.Modules.Execution.Infrastructure;

public sealed class ProviderOptions
{
    public const string SectionName = "Providers";

    public string Mode { get; init; } = "Stub";

    public OpenAiOptions OpenAI { get; init; } = new();
}
