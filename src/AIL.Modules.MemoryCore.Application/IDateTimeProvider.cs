using System;

namespace AIL.Modules.MemoryCore.Application;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
