using AIL.Modules.MemoryCore.Application;
using System;

namespace AIL.Modules.MemoryCore.Infrastructure;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
