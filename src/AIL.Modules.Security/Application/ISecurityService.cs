using AIL.Modules.Security.Domain;

namespace AIL.Modules.Security.Application;

public interface ISecurityService
{
    /// <summary>
    /// Validates whether the current request has a valid tenant and is allowed to execute.
    /// </summary>
    /// <param name="tenantId">The tenant identifier extracted from the incoming request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A decision indicating whether execution is allowed.</returns>
    Task<ExecutionAccessDecision> EvaluateAccessAsync(TenantId? tenantId, CancellationToken cancellationToken = default);
}
