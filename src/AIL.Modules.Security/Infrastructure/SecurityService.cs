using AIL.Modules.Security.Application;
using AIL.Modules.Security.Domain;

namespace AIL.Modules.Security.Infrastructure;

internal sealed class SecurityService : ISecurityService
{
    public Task<ExecutionAccessDecision> EvaluateAccessAsync(TenantId? tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId is null || string.IsNullOrWhiteSpace(tenantId.Value))
        {
            return Task.FromResult(ExecutionAccessDecision.Deny("Tenant identifier is missing."));
        }

        // TODO: Replace with real tenant validation & allowlist checks.
        return Task.FromResult(ExecutionAccessDecision.Allow());
    }
}
