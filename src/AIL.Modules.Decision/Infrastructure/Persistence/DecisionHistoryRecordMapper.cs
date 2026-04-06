using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AIL.Modules.Decision.Application;

namespace AIL.Modules.Decision.Infrastructure.Persistence;

internal static class DecisionHistoryRecordMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static DecisionHistoryEntity ToEntity(DecisionHistoryRecord r)
    {
        return new DecisionHistoryEntity
        {
            Id = r.Id,
            TenantId = r.TenantId,
            CorrelationGroupId = r.CorrelationGroupId,
            ExecutionInstanceId = r.ExecutionInstanceId,
            DecisionType = r.DecisionType,
            SubjectType = r.SubjectType,
            SubjectId = r.SubjectId,
            SelectedStrategyKey = r.SelectedStrategyKey,
            SelectedOptionId = r.SelectedOptionId,
            ConfidenceTier = r.ConfidenceTier,
            PolicyKey = r.PolicyKey,
            ReasonSummary = r.ReasonSummary,
            ConsideredStrategiesJson = JsonSerializer.Serialize(r.ConsideredStrategies.ToArray(), JsonOptions),
            UsedMemory = r.UsedMemory,
            MemoryItemCount = r.MemoryItemCount,
            MemoryInfluenceSummary = r.MemoryInfluenceSummary,
            OptionsJson = JsonSerializer.Serialize(r.Options.ToArray(), JsonOptions),
            Outcome = r.Outcome,
            CreatedAtUtc = r.CreatedAtUtc,
        };
    }

    public static void UpdateEntity(DecisionHistoryEntity e, DecisionHistoryRecord r)
    {
        e.TenantId = r.TenantId;
        e.CorrelationGroupId = r.CorrelationGroupId;
        e.ExecutionInstanceId = r.ExecutionInstanceId;
        e.DecisionType = r.DecisionType;
        e.SubjectType = r.SubjectType;
        e.SubjectId = r.SubjectId;
        e.SelectedStrategyKey = r.SelectedStrategyKey;
        e.SelectedOptionId = r.SelectedOptionId;
        e.ConfidenceTier = r.ConfidenceTier;
        e.PolicyKey = r.PolicyKey;
        e.ReasonSummary = r.ReasonSummary;
        e.ConsideredStrategiesJson = JsonSerializer.Serialize(r.ConsideredStrategies.ToArray(), JsonOptions);
        e.UsedMemory = r.UsedMemory;
        e.MemoryItemCount = r.MemoryItemCount;
        e.MemoryInfluenceSummary = r.MemoryInfluenceSummary;
        e.OptionsJson = JsonSerializer.Serialize(r.Options.ToArray(), JsonOptions);
        e.Outcome = r.Outcome;
        e.CreatedAtUtc = r.CreatedAtUtc;
    }

    public static DecisionHistoryRecord ToRecord(DecisionHistoryEntity e)
    {
        var considered = JsonSerializer.Deserialize<string[]>(e.ConsideredStrategiesJson, JsonOptions)
                         ?? Array.Empty<string>();
        var options = JsonSerializer.Deserialize<DecisionHistoryOptionSnapshot[]>(e.OptionsJson, JsonOptions)
                      ?? Array.Empty<DecisionHistoryOptionSnapshot>();

        return new DecisionHistoryRecord(
            Id: e.Id,
            TenantId: e.TenantId,
            CorrelationGroupId: e.CorrelationGroupId,
            ExecutionInstanceId: e.ExecutionInstanceId,
            DecisionType: e.DecisionType,
            SubjectType: e.SubjectType,
            SubjectId: e.SubjectId,
            SelectedStrategyKey: e.SelectedStrategyKey,
            SelectedOptionId: e.SelectedOptionId,
            ConfidenceTier: e.ConfidenceTier,
            PolicyKey: e.PolicyKey,
            ReasonSummary: e.ReasonSummary,
            ConsideredStrategies: considered,
            UsedMemory: e.UsedMemory,
            MemoryItemCount: e.MemoryItemCount,
            MemoryInfluenceSummary: e.MemoryInfluenceSummary,
            Options: options,
            Outcome: e.Outcome,
            CreatedAtUtc: e.CreatedAtUtc);
    }
}
