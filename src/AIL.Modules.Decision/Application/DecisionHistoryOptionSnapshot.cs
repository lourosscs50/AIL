namespace AIL.Modules.Decision.Application;

/// <summary>
/// Bounded, safe snapshot of one decision option for durable history (no provider or prompt data).
/// </summary>
public sealed record DecisionHistoryOptionSnapshot(
    string OptionId,
    string ConfidenceTier,
    double Strength,
    string RationaleSummary);
