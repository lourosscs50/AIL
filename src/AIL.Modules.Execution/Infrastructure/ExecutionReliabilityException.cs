using AIL.Modules.Execution.Domain;

namespace AIL.Modules.Execution.Infrastructure;

/// <summary>
/// Exception thrown when all execution reliability attempts have failed.
/// </summary>
public sealed class ExecutionReliabilityException : Exception
{
    /// <summary>
    /// Gets the type of failure that prevented execution.
    /// </summary>
    public ExecutionFailureType FailureType { get; }

    public ExecutionReliabilityException(string message, ExecutionFailureType failureType)
        : base(message)
    {
        FailureType = failureType;
    }

    public ExecutionReliabilityException(string message, ExecutionFailureType failureType, Exception innerException)
        : base(message, innerException)
    {
        FailureType = failureType;
    }
}
