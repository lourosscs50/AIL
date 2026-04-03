namespace AIL.Modules.Execution.Infrastructure;

public sealed class OpenAiOptions
{
    public const string SectionName = "Providers:OpenAI";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public string Model { get; init; } = "gpt-4.1-mini";
}
