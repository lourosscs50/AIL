namespace AIL.Modules.PromptRegistry.Domain;

using System.Collections.Generic;

public sealed record PromptResolution(
    string PromptKey,
    string Version,
    string Template,
    IReadOnlyDictionary<string, PromptVariableDefinition> VariableDefinitions);
