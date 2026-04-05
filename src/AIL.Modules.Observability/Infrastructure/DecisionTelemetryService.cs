using AIL.Modules.Observability.Application;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Observability.Infrastructure;

internal sealed class DecisionTelemetryService : IDecisionTelemetryService
{
    private readonly ILogger<DecisionTelemetryService> _logger;

    public DecisionTelemetryService(ILogger<DecisionTelemetryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task TrackAsync(DecisionTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        var level = telemetry.Succeeded ? LogLevel.Information : LogLevel.Warning;

        _logger.Log(
            level,
            "Decision telemetry | TenantId={TenantId} DecisionType={DecisionType} Strategy={Strategy} PolicyKey={PolicyKey} " +
            "UsedMemory={UsedMemory} MemoryItemCount={MemoryItemCount} Candidates={Candidates} Considered={Considered} " +
            "Duration={DurationMs}ms Succeeded={Succeeded}",
            telemetry.TenantId,
            telemetry.DecisionType,
            telemetry.SelectedStrategyKey,
            telemetry.PolicyKey,
            telemetry.UsedMemory,
            telemetry.MemoryItemCount,
            telemetry.CandidateStrategyCount,
            telemetry.ConsideredStrategyCount,
            telemetry.DurationMs,
            telemetry.Succeeded);

        return Task.CompletedTask;
    }
}
