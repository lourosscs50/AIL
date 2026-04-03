using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

public interface IProviderSelectionService
{
    /// <summary>
    /// Selects the best provider/model pair (and optional fallback) for the given capability.
    /// </summary>
    /// <param name="capabilityKey">Capability being executed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ProviderSelectionResult> SelectAsync(string capabilityKey, CancellationToken cancellationToken = default);
}
