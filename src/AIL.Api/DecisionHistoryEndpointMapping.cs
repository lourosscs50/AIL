using System.Linq;
using AIL.Api.Contracts;
using AIL.Modules.Decision.Application;

namespace AIL.Api;

/// <summary>
/// Maps application decision history records to API response DTOs.
/// </summary>
public static class DecisionHistoryEndpointMapping
{
    public static DecisionHistoryItemResponse ToItemResponse(DecisionHistoryRecord r) =>
        new(
            Id: r.Id,
            TenantId: r.TenantId,
            CorrelationGroupId: r.CorrelationGroupId,
            DecisionType: r.DecisionType,
            SubjectType: r.SubjectType,
            SubjectId: r.SubjectId,
            SelectedStrategyKey: r.SelectedStrategyKey,
            SelectedOptionId: r.SelectedOptionId,
            ConfidenceTier: r.ConfidenceTier,
            PolicyKey: r.PolicyKey,
            ReasonSummary: r.ReasonSummary,
            ConsideredStrategies: r.ConsideredStrategies,
            UsedMemory: r.UsedMemory,
            MemoryItemCount: r.MemoryItemCount,
            Options: r.Options.Select(o => new DecisionHistoryOptionItemResponse(
                o.OptionId,
                o.ConfidenceTier,
                o.Strength,
                o.RationaleSummary)).ToList(),
            Outcome: r.Outcome,
            CreatedAtUtc: r.CreatedAtUtc);
}
