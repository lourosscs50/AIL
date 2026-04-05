namespace AIL.Api.Contracts;

public sealed record DecideOptionResponse(
    string OptionId,
    string Confidence,
    double Strength,
    string RationaleSummary);
