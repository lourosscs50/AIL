using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = AIL.Modules.ContextEngine.Domain.ExecutionContext;

namespace AIL.Modules.ContextEngine.Application;

public interface IContextEngineService
{
    Task<ExecutionContext> BuildContextAsync(
        IReadOnlyList<string> referenceIds,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}
