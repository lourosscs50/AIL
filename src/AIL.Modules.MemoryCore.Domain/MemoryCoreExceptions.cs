using System;

namespace AIL.Modules.MemoryCore.Domain;

public class MemoryCoreDomainException : Exception
{
    public MemoryCoreDomainException(string message) : base(message)
    {
    }

    public MemoryCoreDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class InvalidMemoryRecordException : MemoryCoreDomainException
{
    public InvalidMemoryRecordException(string message) : base(message)
    {
    }
}
