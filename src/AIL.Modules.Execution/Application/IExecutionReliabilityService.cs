namespace AIL.Modules.Execution.Application;

/// <summary>
/// Reliability service that wraps provider execution with timeout, retry, and fallback policies.
/// Ensures controlled failure behavior and structured failure classification.
/// </summary>
public interface IExecutionReliabilityService
{
    /// <summary>
    /// Executes a provider request with timeout, retry, and fallback support.
    /// 
    /// Flow:
    /// 1. Attempt primary provider/model with timeout
    /// 2. On timeout or transient failure, retry once (if within max retries)
    /// 3. If primary fails and fallback is allowed, attempt fallback provider/model
    /// 4. Classify failures and record telemetry/audit signals
    /// </summary>
    /// <param name="request">The execution request with provider/model preferences and fallback configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provider execution result with outcome (success, retry used, fallback used, or failure)</returns>
    Task<ProviderExecutionResult> ExecuteWithReliabilityAsync(
        ProviderExecutionRequest request,
        CancellationToken cancellationToken = default);
}
