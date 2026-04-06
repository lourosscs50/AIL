using System;
using System.Collections.Generic;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryRecorderTests
{
    [Fact]
    public void TryRecord_ReturnsNull_OnStoreFailure_WithoutThrowing()
    {
        var store = new ThrowingDecisionHistoryStore();
        var recorder = new DecisionHistoryRecorder(store, NullLogger<DecisionHistoryRecorder>.Instance);
        var request = new DecisionRequest(
            Guid.NewGuid(),
            "t",
            "s",
            "id",
            null,
            null,
            false,
            null,
            null,
            null,
            null);
        var result = new DecisionResult(
            "t",
            "k",
            DecisionConfidence.Low,
            "r",
            new[] { "k" },
            false,
            0,
            new[] { new DecisionOption("k", DecisionConfidence.Low, 0.1, "x") },
            "t",
            null);

        var id = recorder.TryRecord(request, result);

        Assert.Null(id);
    }

    private sealed class ThrowingDecisionHistoryStore : IDecisionHistoryStore
    {
        public void Put(DecisionHistoryRecord record) => throw new InvalidOperationException("disk full");

        public DecisionHistoryRecord? TryGet(Guid tenantId, Guid decisionId) => null;

        public (IReadOnlyList<DecisionHistoryRecord> Items, int TotalCount) List(DecisionHistoryListQuery query) =>
            (Array.Empty<DecisionHistoryRecord>(), 0);
    }
}
