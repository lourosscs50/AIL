using System;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionOption(
    string OptionId,
    DecisionConfidence Confidence,
    double Strength,
    string RationaleSummary);

