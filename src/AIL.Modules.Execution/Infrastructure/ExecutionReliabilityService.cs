using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Domain;
using AIL.Modules.Observability.Application;
using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

/// <summary>
/// Implements reliability policies around provider execution.
/// Provides timeout, retry, and fallback handling to ensure controlled failure behavior.
/// </summary>
internal sealed class ExecutionReliabilityService : IExecutionReliabilityService
{
    private readonly IProviderExecutionGatewayProvider _gatewayResolver;
    private readonly IExecutionTelemetryService _telemetry;
    private readonly IAuditService _audit;

    /// <summary>
    /// Default timeout for provider execution (30 seconds).
    /// </summary>
    private const int DefaultTimeoutMs = 30000;

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    private const int MaxRetryAttempts = 1;

    private sealed class AttemptResult
    {
        public ProviderExecutionResult? Result { get; set; }
        public ExecutionFailureType FailureType { get; set; }
    }

    public ExecutionReliabilityService(
        IProviderExecutionGatewayProvider gatewayResolver,
        IExecutionTelemetryService telemetry,
        IAuditService audit)
    {
        _gatewayResolver = gatewayResolver;
        _telemetry = telemetry;
        _audit = audit;
    }

    public async Task<ProviderExecutionResult> ExecuteWithReliabilityAsync(
        ProviderExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int retryCount = 0;

        // Attempt 1: Primary provider
        var result1 = await AttemptProviderExecutionAsync(
            request,
            providerKey: request.PrimaryProviderKey,
            modelKey: request.PrimaryModelKey,
            attempt: 1,
            cancellationToken);

        if (result1.Result != null)
        {
            stopwatch.Stop();
            // Success on first attempt
            await EmitExecutionTelemetryAsync(request, result1.Result, retryCount, false, success: true, cancellationToken);
            return result1.Result;
        }

        // Attempt 2: Retry if failure is transient or timeout
        if (IsRetryable(result1.FailureType) && retryCount < MaxRetryAttempts)
        {
            retryCount++;
            var result2 = await AttemptProviderExecutionAsync(
                request,
                providerKey: request.PrimaryProviderKey,
                modelKey: request.PrimaryModelKey,
                attempt: 2,
                cancellationToken);

            if (result2.Result != null)
            {
                stopwatch.Stop();
                // Success on retry
                await EmitRetryTelemetryAsync(request, result2.Result, retryCount, cancellationToken);
                return result2.Result;
            }

            result1 = result2;  // Update failure type from retry
        }

        // Attempt 3: Fallback provider if allowed and primary/retry failed
        if (request.FallbackAllowed && IsRetryable(result1.FailureType) && !string.IsNullOrWhiteSpace(request.FallbackProviderKey))
        {
            var result3 = await AttemptProviderExecutionAsync(
                request,
                providerKey: request.FallbackProviderKey,
                modelKey: request.FallbackModelKey,
                attempt: 3,
                cancellationToken);

            if (result3.Result != null)
            {
                stopwatch.Stop();
                // Success on fallback
                var fallbackResult = result3.Result with { UsedFallback = true };
                await EmitFallbackTelemetryAsync(request, fallbackResult, cancellationToken);
                return fallbackResult;
            }
        }

        // All attempts failed
        stopwatch.Stop();
        await EmitFailureTelemetryAsync(request, result1.FailureType, retryCount, cancellationToken);

        throw new ExecutionReliabilityException(
            $"All execution attempts failed. Attempted {1 + retryCount} time(s). " +
            $"Failure type: {result1.FailureType}. Fallback allowed: {request.FallbackAllowed}.",
            result1.FailureType);
    }

    /// <summary>
    /// Attempts a single provider execution with timeout.
    /// Returns result information with nullable result and failure type.
    /// </summary>
    private async Task<AttemptResult> AttemptProviderExecutionAsync(
        ProviderExecutionRequest request,
        string providerKey,
        string? modelKey,
        int attempt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DefaultTimeoutMs);

            var gateway = _gatewayResolver.Resolve(providerKey);

            var executionRequest = request with
            {
                PrimaryProviderKey = providerKey,
                PrimaryModelKey = modelKey ?? request.PrimaryModelKey,
            };

            var result = await gateway.ExecuteAsync(executionRequest, timeoutCts.Token);
            return new AttemptResult { Result = result, FailureType = ExecutionFailureType.None };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout (not external cancellation)
            return new AttemptResult { Result = null, FailureType = ExecutionFailureType.Timeout };
        }
        catch (HttpRequestException ex)
        {
            // HTTP-level failure; classify based on status code if available
            var failureType = ClassifyHttpFailure(ex);
            return new AttemptResult { Result = null, FailureType = failureType };
        }
        catch (ExecutionReliabilityException ex)
        {
            // Preserve the failure type already determined by the gateway.
            return new AttemptResult { Result = null, FailureType = ex.FailureType };
        }
        catch (Exception)
        {
            // Other exceptions; assume non-transient for safety
            return new AttemptResult { Result = null, FailureType = ExecutionFailureType.NonTransientFailure };
        }
    }


    /// <summary>
    /// Determines if a failure type is retryable.
    /// </summary>
    private static bool IsRetryable(ExecutionFailureType failureType)
    {
        return failureType is ExecutionFailureType.Timeout
            or ExecutionFailureType.TransientFailure;
    }

    /// <summary>
    /// Classifies HTTP-related failures.
    /// </summary>
    private static ExecutionFailureType ClassifyHttpFailure(HttpRequestException ex)
    {
        // Examine status code if available
        if (ex.StatusCode.HasValue)
        {
            var code = (int)ex.StatusCode.Value;

            return code switch
            {
                401 or 403 => ExecutionFailureType.AccessDenied,
                400 or 422 => ExecutionFailureType.BadRequest,
                408 or 504 => ExecutionFailureType.Timeout,
                429 or 503 => ExecutionFailureType.TransientFailure,  // Rate limit, service unavailable
                _ => ExecutionFailureType.NonTransientFailure,
            };
        }

        // No status code; assume transient network issue
        return ExecutionFailureType.TransientFailure;
    }

    /// <summary>
    /// Emits telemetry for successful execution (no retry or fallback).
    /// </summary>
    private async Task EmitExecutionTelemetryAsync(
        ProviderExecutionRequest request,
        ProviderExecutionResult result,
        int retryCount,
        bool usedFallback,
        bool success,
        CancellationToken cancellationToken)
    {
        await _telemetry.TrackAsync(new ExecutionTelemetry(
            TenantId: request.TenantId,
            CapabilityKey: request.CapabilityKey,
            PromptKey: request.PromptKey,
            PromptVersion: request.PromptVersion,
            PolicyKey: null,  // Caller will fill this
            ProviderKey: result.ProviderKey,
            ModelKey: result.ModelKey,
            UsedFallback: usedFallback,
            InputTokenCount: result.InputTokenCount,
            OutputTokenCount: result.OutputTokenCount,
            DurationMs: 0,  // Caller will fill this
            Succeeded: success),
            cancellationToken);
    }

    /// <summary>
    /// Emits telemetry indicating a retry occurred and succeeded.
    /// </summary>
    private async Task EmitRetryTelemetryAsync(
        ProviderExecutionRequest request,
        ProviderExecutionResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        // Emit telemetry with a marker noting retry was used
        await _telemetry.TrackAsync(new ExecutionTelemetry(
            TenantId: request.TenantId,
            CapabilityKey: request.CapabilityKey,
            PromptKey: request.PromptKey,
            PromptVersion: request.PromptVersion,
            PolicyKey: null,
            ProviderKey: result.ProviderKey,
            ModelKey: result.ModelKey,
            UsedFallback: false,
            InputTokenCount: result.InputTokenCount,
            OutputTokenCount: result.OutputTokenCount,
            DurationMs: 0,
            Succeeded: true),
            cancellationToken);
    }

    /// <summary>
    /// Emits telemetry indicating fallback was used.
    /// </summary>
    private async Task EmitFallbackTelemetryAsync(
        ProviderExecutionRequest request,
        ProviderExecutionResult result,
        CancellationToken cancellationToken)
    {
        await _telemetry.TrackAsync(new ExecutionTelemetry(
            TenantId: request.TenantId,
            CapabilityKey: request.CapabilityKey,
            PromptKey: request.PromptKey,
            PromptVersion: request.PromptVersion,
            PolicyKey: null,
            ProviderKey: result.ProviderKey,
            ModelKey: result.ModelKey,
            UsedFallback: true,
            InputTokenCount: result.InputTokenCount,
            OutputTokenCount: result.OutputTokenCount,
            DurationMs: 0,
            Succeeded: true),
            cancellationToken);
    }

    /// <summary>
    /// Emits telemetry for execution failure.
    /// </summary>
    private async Task EmitFailureTelemetryAsync(
        ProviderExecutionRequest request,
        ExecutionFailureType failureType,
        int retryCount,
        CancellationToken cancellationToken)
    {
        await _telemetry.TrackAsync(new ExecutionTelemetry(
            TenantId: request.TenantId,
            CapabilityKey: request.CapabilityKey,
            PromptKey: request.PromptKey,
            PromptVersion: request.PromptVersion,
            PolicyKey: null,
            ProviderKey: "unknown",
            ModelKey: "unknown",
            UsedFallback: false,
            InputTokenCount: null,
            OutputTokenCount: null,
            DurationMs: 0,
            Succeeded: false),
            cancellationToken);
    }
}

