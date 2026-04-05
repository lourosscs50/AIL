using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure;

internal static class DecisionConfidenceMapper
{
    /// <summary>
    /// Maps a raw decision score to a confidence level using calibrated thresholds.
    /// Scores are normalized (clamped) and then mapped deterministically.
    /// </summary>
    /// <param name="score">Raw decision score from strategy evaluation.</param>
    /// <returns>Confidence level using calibrated thresholds.</returns>
    public static DecisionConfidence FromScore(int score)
    {
        var normalized = ConfidenceCalibrationRules.NormalizeScore(score);
        return ConfidenceCalibrationRules.MapScoreToConfidence(normalized);
    }
}
