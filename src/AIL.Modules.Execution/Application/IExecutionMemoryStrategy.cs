using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

/// <summary>
/// Defines a strategy for determining whether memory should be used during execution.
/// </summary>
public interface IExecutionMemoryStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given execution context.
    /// </summary>
    bool CanHandle(ExecutionMemoryStrategyContext context);

    /// <summary>
    /// Gets the memory decision for the given execution context.
    /// </summary>
    Task<ExecutionMemoryStrategyDecision> GetDecisionAsync(ExecutionMemoryStrategyContext context, CancellationToken cancellationToken = default);
}