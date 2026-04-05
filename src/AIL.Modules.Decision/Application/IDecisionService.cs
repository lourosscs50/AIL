using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Application;

public interface IDecisionService
{
    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
