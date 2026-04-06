using System;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using KnownSummaries = AIL.Modules.Decision.Domain.KnownMemoryInfluenceSummaries;
using Xunit;

namespace AIL.Api.Tests;

public sealed class DecisionHistoryEndpointMappingTests
{
    [Fact]
    public void ToListItem_And_ToItem_Share_CoreFields_FromSameRecord()
    {
        var id = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var cg = Guid.NewGuid();
        var ex = Guid.NewGuid();
        var at = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var r = new DecisionHistoryRecord(
            Id: id,
            TenantId: tenant,
            CorrelationGroupId: cg,
            ExecutionInstanceId: ex,
            DecisionType: "dtype",
            SubjectType: "st",
            SubjectId: "sid",
            SelectedStrategyKey: "sel",
            SelectedOptionId: "sel",
            ConfidenceTier: DecisionConfidence.Low.ToString(),
            PolicyKey: "pk",
            ReasonSummary: "operator-safe summary",
            ConsideredStrategies: new[] { "a", "b" },
            UsedMemory: true,
            MemoryItemCount: 2,
            MemoryInfluenceSummary: KnownSummaries.NoMemory,
            Options: new[]
            {
                new DecisionHistoryOptionSnapshot("sel", DecisionConfidence.Low.ToString(), 0.5, "why")
            },
            Outcome: "Succeeded",
            CreatedAtUtc: at);

        var list = DecisionHistoryEndpointMapping.ToListItemResponse(r);
        var detail = DecisionHistoryEndpointMapping.ToItemResponse(r);

        Assert.Equal(list.Id, detail.Id);
        Assert.Equal(list.TenantId, detail.TenantId);
        Assert.Equal(list.CorrelationGroupId, detail.CorrelationGroupId);
        Assert.Equal(list.ExecutionInstanceId, detail.ExecutionInstanceId);
        Assert.Equal(list.DecisionType, detail.DecisionType);
        Assert.Equal(list.SubjectType, detail.SubjectType);
        Assert.Equal(list.SubjectId, detail.SubjectId);
        Assert.Equal(list.SelectedStrategyKey, detail.SelectedStrategyKey);
        Assert.Equal(list.SelectedOptionId, detail.SelectedOptionId);
        Assert.Equal(list.ConfidenceTier, detail.ConfidenceTier);
        Assert.Equal(list.PolicyKey, detail.PolicyKey);
        Assert.Equal(list.UsedMemory, detail.UsedMemory);
        Assert.Equal(list.MemoryItemCount, detail.MemoryItemCount);
        Assert.Equal(list.MemoryInfluenceSummary, detail.MemoryInfluenceSummary);
        Assert.Equal(list.Outcome, detail.Outcome);
        Assert.Equal(list.CreatedAtUtc, detail.CreatedAtUtc);

        Assert.NotEmpty(detail.Options);
        Assert.NotEmpty(detail.ConsideredStrategies);
        Assert.NotNull(detail.ReasonSummary);
    }
}
