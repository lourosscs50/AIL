using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Observability.Application;

public interface IExecutionTelemetryService
{
    Task TrackAsync(ExecutionTelemetry telemetry, CancellationToken cancellationToken = default);
}
