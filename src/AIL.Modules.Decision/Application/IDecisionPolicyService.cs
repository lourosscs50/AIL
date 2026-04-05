using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Application;

public interface IDecisionPolicyService
{
    Task<DecisionPolicy> ResolvePolicyAsync(string decisionType, CancellationToken cancellationToken = default);
}
