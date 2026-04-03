using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.ProviderRegistry.Application;

public interface IProviderRegistryService
{
    /// <summary>
    /// Returns all known provider definitions.
    /// </summary>
    Task<IReadOnlyList<Domain.ProviderDefinition>> GetAllProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to resolve a provider by key.
    /// </summary>
    Task<Domain.ProviderDefinition?> ResolveProviderAsync(string providerKey, CancellationToken cancellationToken = default);
}
