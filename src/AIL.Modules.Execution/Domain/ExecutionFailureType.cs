namespace AIL.Modules.Execution.Domain;

/// <summary>
/// Classifies the type of failure that occurred during execution.
/// Distinguishes between configuration errors, transient failures, and timeouts
/// to enable targeted retry and fallback strategies.
/// </summary>
public enum ExecutionFailureType
{
    /// <summary>
    /// No failure; execution succeeded.
    /// </summary>
    None = 0,

    /// <summary>
    /// Request denied due to authentication/authorization.
    /// Not retryable; not fallback-eligible.
    /// </summary>
    AccessDenied = 1,

    /// <summary>
    /// Request malformed or configuration error (e.g., invalid API key, unsupported model).
    /// Not retryable; not fallback-eligible unless fallback is explicitly enabled.
    /// </summary>
    BadRequest = 2,

    /// <summary>
    /// Provider call exceeded configured timeout.
    /// Retryable; fallback-eligible.
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// Transient provider failure (e.g., temporary service unavailability, rate limit).
    /// Retryable; fallback-eligible.
    /// </summary>
    TransientFailure = 4,

    /// <summary>
    /// Non-transient provider failure (e.g., persistent service degradation).
    /// Not retryable; fallback-eligible.
    /// </summary>
    NonTransientFailure = 5,
}
