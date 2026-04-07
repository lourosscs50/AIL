namespace AIL.Modules.Decision.Domain;

/// <summary>
/// Deterministic confidence tier derived from evaluation scores.
/// This type lives in the inward-most Decision.Domain layer and is safe for inner Decision contracts.
/// Decision.Domain must not take dependencies on Decision.Application, Decision.Infrastructure, or API types.
/// </summary>
public enum DecisionConfidence
{
    Low = 0,
    Medium = 1,
    High = 2
}
