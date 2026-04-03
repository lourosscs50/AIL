using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

public interface IProviderExecutionGateway
{
    /// <summary>
    /// Provider key that identifies this gateway.
    /// </summary>
    string ProviderKey { get; }

    Task<ProviderExecutionResult> ExecuteAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default);
}
