using System.Collections.Generic;

namespace AIL.Modules.Execution.Application;

public sealed record MemoryExecutionContext(IReadOnlyList<MemoryContextItem> Items);
