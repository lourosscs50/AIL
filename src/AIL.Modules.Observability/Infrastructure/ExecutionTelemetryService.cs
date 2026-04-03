using AIL.Modules.Observability.Application;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Observability.Infrastructure;

internal sealed class ExecutionTelemetryService : IExecutionTelemetryService
{
    private readonly ILogger<ExecutionTelemetryService> _logger;

    public ExecutionTelemetryService(ILogger<ExecutionTelemetryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task TrackAsync(ExecutionTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        var level = telemetry.Succeeded ? LogLevel.Information : LogLevel.Warning;

        _logger.Log(
            level,
            "Execution telemetry | TenantId={TenantId} Capability={Capability} Provider={Provider} Model={Model} " +
            "Tokens=({InputTokens}/{OutputTokens}) Duration={DurationMs}ms Succeeded={Succeeded} Fallback={Fallback}",
            telemetry.TenantId,
            telemetry.CapabilityKey,
            telemetry.ProviderKey,
            telemetry.ModelKey,
            telemetry.InputTokenCount,
            telemetry.OutputTokenCount,
            telemetry.DurationMs,
            telemetry.Succeeded,
            telemetry.UsedFallback);

        return Task.CompletedTask;
    }
}
