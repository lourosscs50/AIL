using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Derives a deterministic, high-level memory influence label from evaluation outcomes (no raw memory payloads).
/// </summary>
internal static class MemoryInfluenceSummaryResolver
{
    public static string Resolve(
        bool usedMemory,
        int memoryItemCount,
        (string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal) winner,
        IReadOnlyList<(string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal)> evaluated)
    {
        if (!usedMemory)
            return KnownMemoryInfluenceSummaries.NoMemory;

        if (memoryItemCount == 0)
            return KnownMemoryInfluenceSummaries.MemoryEmpty;

        var memoryStrategies = evaluated
            .Where(x =>
                x.RegistryKey == KnownDecisionStrategyKeys.MemoryInformed ||
                x.RegistryKey == KnownDecisionStrategyKeys.DecisionContinuity)
            .ToList();

        if (memoryStrategies.Count >= 2)
        {
            var distinctSuggestions = memoryStrategies
                .Select(x => x.Eval.SuggestedStrategyKey)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctSuggestions.Count > 1)
                return KnownMemoryInfluenceSummaries.MemoryConflict;
        }

        if (winner.RegistryKey == KnownDecisionStrategyKeys.MemoryInformed)
            return KnownMemoryInfluenceSummaries.MemoryReinforced;

        if (winner.RegistryKey == KnownDecisionStrategyKeys.DecisionContinuity)
            return KnownMemoryInfluenceSummaries.MemoryConsistent;

        if (memoryStrategies.Count > 0)
            return KnownMemoryInfluenceSummaries.MemoryNeutral;

        return KnownMemoryInfluenceSummaries.MemoryNeutral;
    }
}
