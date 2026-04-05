namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>Operator-safe prompt registry resolution facts (identifiers only; never template body).</summary>
public sealed record ExecutionPromptVisibility(
    string PromptKey,
    string? PromptVersion,
    bool ResolutionSucceeded);
