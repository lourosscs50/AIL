using AIL.Modules.Execution.Application.Visibility;
using AIL.Modules.Execution.Infrastructure;
using Xunit;

namespace AIL.Modules.Execution.Tests;

public sealed class InMemoryExecutionVisibilityReadStoreListTests
{
    [Fact]
    public void ListByCompletedAtDescending_Orders_Newest_First_And_Pages()
    {
        var store = new InMemoryExecutionVisibilityReadStore();
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        store.Put(MinimalModel(older, DateTime.UtcNow.AddHours(-2)));
        store.Put(MinimalModel(newer, DateTime.UtcNow.AddHours(-1)));

        var (page1, total) = store.ListByCompletedAtDescending(1, 1);
        Assert.Equal(2, total);
        Assert.Single(page1);
        Assert.Equal(newer, page1[0].Trace.ExecutionInstanceId);

        var (page2, total2) = store.ListByCompletedAtDescending(2, 1);
        Assert.Equal(2, total2);
        Assert.Single(page2);
        Assert.Equal(older, page2[0].Trace.ExecutionInstanceId);
    }

    private static ExecutionVisibilityReadModel MinimalModel(Guid executionInstanceId, DateTime completedAtUtc) =>
        new(
            CapabilityKey: "c",
            Trace: new ExecutionTraceVisibility(
                TraceThreadId: null,
                CorrelationGroupId: null,
                ExecutionInstanceId: executionInstanceId,
                RelatedEntityIds: Array.Empty<Guid>()),
            Prompt: new ExecutionPromptVisibility("p", null, true),
            Memory: new ExecutionMemoryVisibility(false, null, null),
            Reliability: new ExecutionReliabilityVisibility(
                false,
                null,
                null,
                "stub",
                "m1",
                "stub",
                "m1",
                null,
                null),
            Explanation: new ExecutionExplanationVisibility(false, null, null),
            OutputSummary: "ok",
            StartedAtUtc: completedAtUtc.AddMinutes(-1),
            CompletedAtUtc: completedAtUtc,
            Status: "Succeeded",
            SafeErrorSummary: null);
}
