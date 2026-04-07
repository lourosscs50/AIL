namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Bounded internal observability labels for decision execution lifecycle stages and failures.
/// These values are internal-only and must not be surfaced as public API contract fields.
/// </summary>
internal static class DecisionExecutionObservability
{
    private static readonly Stage[] SuccessOrder =
    {
        Stage.EvaluationStarted,
        Stage.StrategiesEvaluated,
        Stage.WinnerSelected,
        Stage.PolicyFiltered,
        Stage.FallbackApplied,
        Stage.Completed,
    };

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

    /// <summary>
    /// Enforces deterministic stage sequencing for a single decision execution:
    /// required start at <see cref="Stage.EvaluationStarted"/>, optional <see cref="Stage.FallbackApplied"/>,
    /// terminal <see cref="Stage.Completed"/> or <see cref="Stage.Failed"/>, and no duplicates.
    /// </summary>
    internal sealed class StageSequenceGuard
    {
        private int _highestOrderIndex = -1;
        private bool _fallbackSeen;
        private bool _completedSeen;
        private bool _failedSeen;

        internal void RecordStage(Stage stage)
        {
            if (_completedSeen || _failedSeen)
                throw new InvalidOperationException("No execution stages are allowed after terminal stage.");

            if (stage == Stage.Failed)
            {
                _failedSeen = true;
                return;
            }

            var idx = Array.IndexOf(SuccessOrder, stage);
            if (idx < 0)
                throw new InvalidOperationException($"Unknown stage: {stage}.");

            if (stage == Stage.FallbackApplied)
            {
                if (_fallbackSeen)
                    throw new InvalidOperationException("FallbackApplied can be emitted at most once.");
                _fallbackSeen = true;
            }

            if (idx < _highestOrderIndex)
                throw new InvalidOperationException($"Out-of-order stage emission: {stage}.");

            if (idx == _highestOrderIndex)
                throw new InvalidOperationException($"Duplicate stage emission: {stage}.");

            _highestOrderIndex = idx;

            if (stage == Stage.Completed)
                _completedSeen = true;
        }
    }
}
