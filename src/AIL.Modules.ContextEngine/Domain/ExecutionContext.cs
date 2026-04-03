using System.Collections.Generic;

namespace AIL.Modules.ContextEngine.Domain;

public sealed record ExecutionContext(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> ReferenceIds);
