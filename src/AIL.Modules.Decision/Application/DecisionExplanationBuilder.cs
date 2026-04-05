namespace AIL.Modules.Decision.Application;

/// <summary>
/// Builds standardized decision explanation text from active decision signals.
/// </summary>
public static class DecisionExplanationBuilder
{
    private static class Vocabulary
    {
        internal const string CandidateMatch = "exact candidate match";
        internal const string Escalation = "escalation or priority signal";
        internal const string MemoryContext = "memory and contextual information";
        internal const string HistoricalContinuity = "consistent historical precedent";
        internal const string DefaultFallback = "default evaluation";
        internal const string GenericSignal = "evaluated signals";

        internal const string SingleSignalTemplate = "Decision influenced by {0}";
        internal const string TwoSignalsTemplate = "Decision informed by {0} and {1}";
        internal const string FallbackExplanation = "Decision based on standard evaluation";
    }

    /// <summary>
    /// Builds a standardized explanation from the provided active signals.
    /// </summary>
    public static string BuildExplanation(IReadOnlyList<DecisionSignal>? signals)
    {
        if (signals is null || signals.Count == 0)
            return Vocabulary.FallbackExplanation;

        var activeSignals = signals
            .Where(signal => signal.IsActive)
            .ToList();

        if (activeSignals.Count == 0)
            return Vocabulary.FallbackExplanation;

        var orderedSignals = activeSignals
            .OrderByDescending(signal => SignalPriority(signal.Type))
            .ThenByDescending(signal => signal.Strength)
            .ThenBy(signal => signal.Type)
            .ToList();

        if (orderedSignals.Count == 0)
            return Vocabulary.FallbackExplanation;

        var primary = GetPhrase(orderedSignals[0].Type);
        if (orderedSignals.Count == 1)
            return string.Format(Vocabulary.SingleSignalTemplate, primary);

        var secondary = GetPhrase(orderedSignals[1].Type);
        return string.Format(Vocabulary.TwoSignalsTemplate, primary, secondary);
    }

    private static string GetPhrase(DecisionSignalType type) => type switch
    {
        DecisionSignalType.CandidateMatch => Vocabulary.CandidateMatch,
        DecisionSignalType.EscalationSignal => Vocabulary.Escalation,
        DecisionSignalType.MemoryContext => Vocabulary.MemoryContext,
        DecisionSignalType.HistoricalContinuity => Vocabulary.HistoricalContinuity,
        DecisionSignalType.DefaultFallback => Vocabulary.DefaultFallback,
        _ => Vocabulary.GenericSignal
    };

    private static int SignalPriority(DecisionSignalType type) => type switch
    {
        DecisionSignalType.CandidateMatch => 100,
        DecisionSignalType.EscalationSignal => 90,
        DecisionSignalType.HistoricalContinuity => 80,
        DecisionSignalType.MemoryContext => 70,
        DecisionSignalType.DefaultFallback => 10,
        _ => 0
    };
}
