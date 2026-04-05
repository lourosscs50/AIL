using System.Collections.Generic;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionMemoryContext(IReadOnlyList<DecisionMemoryContextItem> Items);
