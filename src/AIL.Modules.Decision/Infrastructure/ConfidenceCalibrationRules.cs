using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Defines explicit, deterministic rules for normalizing and calibrating raw decision scores
/// into confidence levels. These thresholds are foundational to advisor safety and must remain stable.
/// </summary>
internal static class ConfidenceCalibrationRules
{
    /// <summary>
    /// Minimum raw score (inclusive) to map to Medium confidence.
    /// Scores below this map to Low confidence.
    /// </summary>
    public const int MediumConfidenceThreshold = 500;

    /// <summary>
    /// Minimum raw score (inclusive) to map to High confidence.
    /// Scores below this map to at most Medium confidence.
    /// </summary>
    public const int HighConfidenceThreshold = 900;

    /// <summary>
    /// Maximum valid raw decision score. Scores beyond this are clamped.
    /// </summary>
    public const int MaxScore = 1000;

    /// <summary>
    /// Minimum valid raw decision score. Scores below this are clamped.
    /// </summary>
    public const int MinScore = 0;

    /// <summary>
    /// Normalizes a raw score by clamping it to valid bounds [MinScore, MaxScore].
    /// </summary>
    /// <param name="score">Raw decision score from strategy evaluation.</param>
    /// <returns>Normalized score within [MinScore, MaxScore].</returns>
    public static int NormalizeScore(int score) =>
        Math.Clamp(score, MinScore, MaxScore);

    /// <summary>
    /// Maps a normalized score to a confidence level using calibrated thresholds.
    /// </summary>
    /// <param name="normalizedScore">Score within [MinScore, MaxScore].</param>
    /// <returns>Confidence level: High if normalizedScore >= HighConfidenceThreshold,
    /// Medium if >= MediumConfidenceThreshold, Low otherwise.</returns>
    public static DecisionConfidence MapScoreToConfidence(int normalizedScore) =>
        normalizedScore >= HighConfidenceThreshold
            ? DecisionConfidence.High
            : normalizedScore >= MediumConfidenceThreshold
            ? DecisionConfidence.Medium
            : DecisionConfidence.Low;
}
