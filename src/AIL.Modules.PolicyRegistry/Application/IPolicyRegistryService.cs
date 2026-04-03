using AIL.Modules.PolicyRegistry.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.PolicyRegistry.Application;

public interface IPolicyRegistryService
{
    Task<ExecutionPolicy> ResolvePolicyAsync(string capabilityKey, CancellationToken cancellationToken = default);
}
