using AIL.Modules.ContextEngine.Application;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = AIL.Modules.ContextEngine.Domain.ExecutionContext;

namespace AIL.Modules.ContextEngine.Infrastructure;

internal sealed class ContextEngineService : IContextEngineService
{
    public Task<ExecutionContext> BuildContextAsync(
        IReadOnlyList<string> referenceIds,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        // Placeholder implementation that just returns the inputs as context.
        var context = new ExecutionContext(Variables: variables, ReferenceIds: referenceIds);
        return Task.FromResult(context);
    }
}
