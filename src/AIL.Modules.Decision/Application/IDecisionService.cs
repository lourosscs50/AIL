using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Application;

/// <summary>
/// Application-layer Decision contract used by callers to execute decision use cases.
/// Decision.Application may depend inward on Decision.Domain, but must not depend on API or Decision.Infrastructure concretes.
/// </summary>
public interface IDecisionService
{
    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
