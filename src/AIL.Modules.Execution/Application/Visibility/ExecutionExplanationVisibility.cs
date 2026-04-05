using System.Collections.Generic;

namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>Bounded, operator-safe explanation surface (no chain-of-thought).</summary>
public sealed record ExecutionExplanationVisibility(
    bool ExplanationAvailable,
    string? SummaryText,
    IReadOnlyList<string>? ReasonCodes);
