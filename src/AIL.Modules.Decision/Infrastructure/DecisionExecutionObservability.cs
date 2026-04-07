namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Bounded internal observability labels for decision execution lifecycle stages and failures.
/// These values are internal-only and must not be surfaced as public API contract fields.
/// </summary>
internal static class DecisionExecutionObservability
{
    internal enum Stage
    {
        EvaluationStarted,
        StrategiesEvaluated,
        WinnerSelected,
        PolicyFiltered,
        FallbackApplied,
        Completed,
        Failed,
    }

    internal enum FailureCategory
    {
        Validation,
        PolicyResolution,
        StrategyEvaluation,
        Unexpected,
        Canceled,
    }

    internal static string ToLabel(Stage stage) => stage.ToString();
    internal static string ToLabel(FailureCategory category) => category.ToString();
}
