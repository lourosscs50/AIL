using AIL.Modules.Execution.Application;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class ProviderExecutionGatewayResolver : IProviderExecutionGatewayProvider
{
    private readonly IReadOnlyDictionary<string, IProviderExecutionGateway> _gateways;

    public ProviderExecutionGatewayResolver(IEnumerable<IProviderExecutionGateway> gateways)
    {
        if (gateways is null) throw new ArgumentNullException(nameof(gateways));

        _gateways = gateways
            .GroupBy(g => g.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IProviderExecutionGateway Resolve(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Provider key must be specified.", nameof(providerKey));
        }

        if (_gateways.TryGetValue(providerKey, out var gateway))
        {
            return gateway;
        }

        throw new InvalidOperationException($"No provider gateway registered for key '{providerKey}'.");
    }
}
