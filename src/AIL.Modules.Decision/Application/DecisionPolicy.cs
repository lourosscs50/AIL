using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Application;

public sealed record DecisionPolicy(
    string PolicyKey,
    int MaxOptions = 3,
    DecisionConfidence MinimumConfidence = DecisionConfidence.Low);

