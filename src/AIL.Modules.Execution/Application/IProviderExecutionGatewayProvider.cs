namespace AIL.Modules.Execution.Application;

/// <summary>
/// Resolves a named provider gateway by provider key.
/// </summary>
public interface IProviderExecutionGatewayProvider
{
    /// <summary>
    /// Resolves the gateway for the specified provider key.
    /// Throws if no such provider is registered.
    /// </summary>
    IProviderExecutionGateway Resolve(string providerKey);
}
