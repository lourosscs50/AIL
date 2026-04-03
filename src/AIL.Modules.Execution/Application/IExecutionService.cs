using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

public interface IExecutionService
{
    /// <summary>
    /// Executes an intelligence request and returns a structured response.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
