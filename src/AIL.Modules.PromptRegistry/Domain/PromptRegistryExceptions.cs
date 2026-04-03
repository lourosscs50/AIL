using System;

namespace AIL.Modules.PromptRegistry.Domain;

public class PromptRegistryException : Exception
{
    public PromptRegistryException(string message) : base(message) { }
}

public sealed class PromptNotFoundException : PromptRegistryException
{
    public PromptNotFoundException(string promptKey)
        : base($"Prompt not found for key '{promptKey}'") { }
}

public sealed class PromptVersionNotFoundException : PromptRegistryException
{
    public PromptVersionNotFoundException(string promptKey, string version)
        : base($"Prompt not found for key '{promptKey}' and version '{version}'") { }
}

public sealed class PromptInactiveException : PromptRegistryException
{
    public PromptInactiveException(string promptKey, string? version = null)
        : base($"Prompt '{promptKey}'{(version != null ? $" v{version}" : string.Empty)} is inactive") { }
}

public sealed class PromptAmbiguousException : PromptRegistryException
{
    public PromptAmbiguousException(string promptKey, string version)
        : base($"Duplicate active prompt definitions found for key '{promptKey}', version '{version}'") { }
}

public sealed class PromptValidationException : PromptRegistryException
{
    public PromptValidationException(string message) : base(message) { }
}
