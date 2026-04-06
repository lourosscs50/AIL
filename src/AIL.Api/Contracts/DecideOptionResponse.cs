namespace AIL.Api.Contracts;

/// <summary>
/// One advisory option in a bounded decision response. <see cref="RationaleSummary"/> is a high-level, non-chain-of-thought explanation.
/// </summary>
public sealed record DecideOptionResponse(
    string OptionId,
    string Confidence,
    double Strength,
    string RationaleSummary);