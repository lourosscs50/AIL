using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Decision.Application;

namespace AIL.Modules.Decision.Infrastructure;

internal static class DecisionHistoryRecordBuilder
{
    internal const int MaxSubjectFieldLength = 256;
    internal const int MaxReasonSummaryLength = 2048;
    internal const int MaxOptionRationaleLength = 1024;
    internal const int MaxMemoryInfluenceSummaryLength = 64;

    public static DecisionHistoryRecord Build(Guid id, DecisionRequest request, DecisionResult result, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var options = result.Options.Select(o => new DecisionHistoryOptionSnapshot(
            OptionId: o.OptionId,
            ConfidenceTier: o.Confidence.ToString(),
            Strength: o.Strength,
            RationaleSummary: Truncate(o.RationaleSummary, MaxOptionRationaleLength))).ToList();

        var selectedOptionId = options.Exists(o => o.OptionId == result.SelectedStrategyKey)
            ? result.SelectedStrategyKey
            : null;

        return new DecisionHistoryRecord(
            Id: id,
            TenantId: request.TenantId,
            CorrelationGroupId: request.CorrelationGroupId,
            ExecutionInstanceId: request.ExecutionInstanceId,
            DecisionType: result.DecisionType,
            SubjectType: Truncate(request.SubjectType, MaxSubjectFieldLength),
            SubjectId: Truncate(request.SubjectId, MaxSubjectFieldLength),
            SelectedStrategyKey: result.SelectedStrategyKey,
            SelectedOptionId: selectedOptionId,
            ConfidenceTier: result.Confidence.ToString(),
            PolicyKey: result.PolicyKey,
            ReasonSummary: Truncate(result.ReasonSummary, MaxReasonSummaryLength),
            ConsideredStrategies: result.ConsideredStrategies,
            UsedMemory: result.UsedMemory,
            MemoryItemCount: result.MemoryItemCount,
            MemoryInfluenceSummary: Truncate(result.MemoryInfluenceSummary, MaxMemoryInfluenceSummaryLength),
            Options: options,
            Outcome: "Succeeded",
            CreatedAtUtc: createdAtUtc);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
