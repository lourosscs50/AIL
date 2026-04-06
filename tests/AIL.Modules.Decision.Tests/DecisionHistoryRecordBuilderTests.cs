using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using KnownSummaries = AIL.Modules.Decision.Domain.KnownMemoryInfluenceSummaries;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryRecordBuilderTests
{
    [Fact]
    public void Build_MapsDecisionResultTruth_AndOmitsUnsafeRequestPayloads()
    {
        var tenant = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var execution = Guid.NewGuid();
        var request = new DecisionRequest(
            tenant,
            "dtype",
            "subjT",
            "subjId",
            ContextText: "do-not-persist-context",
            StructuredContext: new Dictionary<string, string> { ["k"] = "do-not-persist-structured" },
            IncludeMemory: false,
            MemoryQuery: null,
            CandidateStrategies: null,
            Metadata: new Dictionary<string, string> { ["secret"] = "meta" },
            CorrelationGroupId: correlation,
            ExecutionInstanceId: execution);

        var options = new List<DecisionOption>
        {
            new("opt_a", DecisionConfidence.High, 0.9, "because a"),
            new("opt_b", DecisionConfidence.Low, 0.2, "because b")
        };

        var result = new DecisionResult(
            DecisionType: "dtype",
            SelectedStrategyKey: "opt_a",
            Confidence: DecisionConfidence.High,
            ReasonSummary: "summary line",
            ConsideredStrategies: new[] { "opt_a", "opt_b" },
            UsedMemory: false,
            MemoryItemCount: 0,
            MemoryInfluenceSummary: KnownSummaries.NoMemory,
            Options: options,
            PolicyKey: "dtype",
            Metadata: request.Metadata);

        var id = Guid.NewGuid();
        var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var record = DecisionHistoryRecordBuilder.Build(id, request, result, at);

        Assert.Equal(id, record.Id);
        Assert.Equal(tenant, record.TenantId);
        Assert.Equal(correlation, record.CorrelationGroupId);
        Assert.Equal(execution, record.ExecutionInstanceId);
        Assert.Equal("dtype", record.DecisionType);
        Assert.Equal("opt_a", record.SelectedStrategyKey);
        Assert.Equal("opt_a", record.SelectedOptionId);
        Assert.Equal(2, record.Options.Count);
        Assert.Equal("Succeeded", record.Outcome);
        Assert.Equal(at, record.CreatedAtUtc);

        var names = typeof(DecisionHistoryRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("ContextText", names);
        Assert.DoesNotContain("StructuredContext", names);
        Assert.DoesNotContain("Metadata", names);
    }

    [Fact]
    public void Build_TruncatesLongSubjectAndRationale()
    {
        var longSubject = new string('x', DecisionHistoryRecordBuilder.MaxSubjectFieldLength + 50);
        var longReason = new string('y', DecisionHistoryRecordBuilder.MaxReasonSummaryLength + 10);
        var longRat = new string('z', DecisionHistoryRecordBuilder.MaxOptionRationaleLength + 5);
        var request = new DecisionRequest(
            Guid.NewGuid(),
            "d",
            longSubject,
            longSubject,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null);

        var result = new DecisionResult(
            "d",
            "k",
            DecisionConfidence.Low,
            longReason,
            new[] { "k" },
            false,
            0,
            KnownSummaries.NoMemory,
            new[] { new DecisionOption("k", DecisionConfidence.Low, 0.1, longRat) },
            "d",
            null);

        var record = DecisionHistoryRecordBuilder.Build(Guid.NewGuid(), request, result, DateTime.UtcNow);
        Assert.Equal(DecisionHistoryRecordBuilder.MaxSubjectFieldLength, record.SubjectType.Length);
        Assert.Equal(DecisionHistoryRecordBuilder.MaxReasonSummaryLength, record.ReasonSummary.Length);
        Assert.Equal(DecisionHistoryRecordBuilder.MaxOptionRationaleLength, record.Options[0].RationaleSummary.Length);
    }
}
