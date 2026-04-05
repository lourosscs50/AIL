using System;
using System.Collections.Generic;

namespace AIL.Modules.MemoryCore.Contracts;

public sealed record UpdateMemoryRequest(
    Guid TenantId,
    Guid MemoryId,
    string Content,
    IReadOnlyDictionary<string, string>? Metadata,
    string Importance);
