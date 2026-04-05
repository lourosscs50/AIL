using AIL.Modules.Execution.Application;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

/// <summary>
/// Default execution memory strategy that makes deterministic decisions based on explicit execution context.
/// </summary>
internal sealed class DefaultExecutionMemoryStrategy : IExecutionMemoryStrategy
{
    public bool CanHandle(ExecutionMemoryStrategyContext context) => true;

    public Task<ExecutionMemoryStrategyDecision> GetDecisionAsync(ExecutionMemoryStrategyContext context, CancellationToken cancellationToken = default)
    {
        // Strategy rules (deterministic, explicit):
        // 1. If a memory key is explicitly provided, enable memory (suggests specific retrieval intent)
        // 2. If a specific scope is provided, enable memory (suggests scoped retrieval intent)  
        // 3. If request is broad/list-only with no key/scope signal, keep memory disabled (avoid unnecessary retrieval)
        // 4. If strategy enables memory and no explicit max is provided, provide a bounded default

        var hasExplicitKey = context.MemoryQuery?.Keys is { Count: > 0 };
        var hasSpecificScope = !string.IsNullOrWhiteSpace(context.MemoryQuery?.ScopeId);
        var isBroadListMode = context.MemoryQuery is not null &&
                             context.MemoryQuery.Keys is null or { Count: 0 } &&
                             string.IsNullOrWhiteSpace(context.MemoryQuery.ScopeId) &&
                             string.IsNullOrWhiteSpace(context.MemoryQuery.MemoryKind);

        if (hasExplicitKey)
        {
            return Task.FromResult(new ExecutionMemoryStrategyDecision(
                ShouldUseMemory: true,
                SuggestedMaxResults: 5, // Bounded default for key-based retrieval
                DecisionReason: "ExplicitKey"));
        }

        if (hasSpecificScope)
        {
            return Task.FromResult(new ExecutionMemoryStrategyDecision(
                ShouldUseMemory: true,
                SuggestedMaxResults: 10, // Higher bound for scoped retrieval
                DecisionReason: "SpecificScope"));
        }

        if (isBroadListMode)
        {
            return Task.FromResult(new ExecutionMemoryStrategyDecision(
                ShouldUseMemory: false,
                SuggestedMaxResults: null,
                DecisionReason: "BroadListMode"));
        }

        // Default: no memory for unspecified cases
        return Task.FromResult(new ExecutionMemoryStrategyDecision(
            ShouldUseMemory: false,
            SuggestedMaxResults: null,
            DecisionReason: "NoSignal"));
    }
}