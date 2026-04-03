using System.Collections.Generic;

namespace AIL.Modules.PromptRegistry.Domain;

public sealed record PromptVariableDefinition(string Name, bool Required);

public sealed record PromptDefinition(
    string PromptKey,
    string Version,
    string Template,
    bool IsActive,
    string? Description,
    IReadOnlyDictionary<string, PromptVariableDefinition> VariableDefinitions);
