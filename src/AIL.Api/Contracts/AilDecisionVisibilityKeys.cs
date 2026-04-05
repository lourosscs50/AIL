namespace AIL.Api.Contracts;

/// <summary>
/// Stable keys for A.I.L. execution rows when projected as <see cref="DecisionVisibilityResponse"/>
/// (aligned with SignalForge <c>DecisionVisibilityKeys</c> naming style).
/// </summary>
public static class AilDecisionVisibilityKeys
{
    public const string CategoryExecution = "ail.execution";

    public static class Types
    {
        public const string IntelligenceSucceeded = "ail.execution.intelligence.succeeded";
        public const string IntelligenceDenied = "ail.execution.intelligence.denied";
        public const string IntelligenceFailed = "ail.execution.intelligence.failed";
        public const string IntelligenceUnknown = "ail.execution.intelligence.unknown";
    }

    public const string StatusSucceeded = "succeeded";
    public const string StatusDenied = "denied";
    public const string StatusFailed = "failed";
}
