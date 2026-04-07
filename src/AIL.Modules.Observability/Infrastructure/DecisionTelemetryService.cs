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
            "Decision telemetry | TenantId={TenantId} DecisionType={DecisionType} Stage={Stage} Strategy={Strategy} Confidence={Confidence} PolicyKey={PolicyKey} " +
            "UsedMemory={UsedMemory} MemoryItemCount={MemoryItemCount} MemoryInfluence={MemoryInfluence} Candidates={Candidates} Considered={Considered} " +
            "FallbackApplied={FallbackApplied} FailureCategory={FailureCategory} Duration={DurationMs}ms Succeeded={Succeeded}",
            telemetry.TenantId,
            telemetry.DecisionType,
            telemetry.ExecutionStage,
            telemetry.SelectedStrategyKey,
            telemetry.ConfidenceTier,
            telemetry.PolicyKey,
            telemetry.UsedMemory,
            telemetry.MemoryItemCount,
            telemetry.MemoryInfluenceSummary,
            telemetry.CandidateStrategyCount,
            telemetry.ConsideredStrategyCount,
            telemetry.FallbackApplied,
            telemetry.FailureCategory,
            telemetry.DurationMs,
            telemetry.Succeeded);

        return Task.CompletedTask;
    }
}
