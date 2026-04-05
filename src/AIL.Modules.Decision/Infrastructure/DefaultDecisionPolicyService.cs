using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Infrastructure;

internal sealed class DefaultDecisionPolicyService : IDecisionPolicyService
{
    public Task<DecisionPolicy> ResolvePolicyAsync(string decisionType, CancellationToken cancellationToken = default)
    {
        var policyKey = string.IsNullOrWhiteSpace(decisionType)
            ? "decision.default"
            : decisionType;

        if (string.IsNullOrWhiteSpace(policyKey))
            throw new System.ArgumentException("PolicyKey is required.", nameof(policyKey));

        var maxOptions = 3;
        if (maxOptions <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(maxOptions), "MaxOptions must be greater than zero.");

        var policy = new DecisionPolicy(
            PolicyKey: policyKey,
            MaxOptions: maxOptions,
            MinimumConfidence: DecisionConfidence.Low);

        return Task.FromResult(policy);
    }
}
