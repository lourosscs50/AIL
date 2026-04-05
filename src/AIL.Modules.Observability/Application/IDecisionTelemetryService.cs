using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Observability.Application;

public interface IDecisionTelemetryService
{
    Task TrackAsync(DecisionTelemetry telemetry, CancellationToken cancellationToken = default);
}
