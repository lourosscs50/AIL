using AIL.Modules.MemoryCore.Domain;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Application;

public sealed record MemoryListResult(
    IReadOnlyList<MemoryRecord> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);