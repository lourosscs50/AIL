using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Application.Visibility;
using AIL.Modules.Execution.Infrastructure;
using Xunit;

namespace AIL.Modules.Execution.Tests;

public sealed class ExecutionVisibilityReadModelBuilderTests
{
    [Fact]
    public void ParseRelatedEntityIds_ParsesGuidsOnly()
    {
        var g = Guid.NewGuid();
        var ids = ExecutionVisibilityReadModelBuilder.ParseRelatedEntityIds(new[] { g.ToString(), "not-a-guid", "" });
        Assert.Single(ids);
        Assert.Equal(g, ids[0]);
    }

    [Fact]
    public void SummarizeOutput_TruncatesLongText()
    {
        var longText = new string('x', ExecutionVisibilityReadModelBuilder.MaxOutputSummaryLength + 50);
        var s = ExecutionVisibilityReadModelBuilder.SummarizeOutput(longText);
        Assert.Equal(ExecutionVisibilityReadModelBuilder.MaxOutputSummaryLength + 1, s.Length);
        Assert.EndsWith("…", s);
    }

    [Fact]
    public void BuildSucceeded_MapsPromptMemoryReliabilityAndTrace()
    {
        var execId = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p1",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string> { entityId.ToString() },
            ExecutionInstanceId: execId,
            TraceThreadId: "trace-hub-1",
            CorrelationGroupId: correlation);

        var selection = new ProviderSelectionResult("primary", "m1", "fb", "m2", true, 100, 5000);
        var result = new ProviderExecutionResult("fb", "m2", "hello", true, 1, 2);

        var model = ExecutionVisibilityReadModelBuilder.BuildSucceeded(
            execId,
            request,
            DateTime.UtcNow.AddSeconds(-1),
            DateTime.UtcNow,
            "policy-x",
            selection,
            result,
            "v9",
            memoryRequested: true,
            memoryItemCount: 3,
            outputText: "full output");

        Assert.Equal("cap", model.CapabilityKey);
        Assert.Equal("trace-hub-1", model.Trace.TraceThreadId);
        Assert.Equal(correlation, model.Trace.CorrelationGroupId);
        Assert.Equal(execId, model.Trace.ExecutionInstanceId);
        Assert.Equal(entityId, Assert.Single(model.Trace.RelatedEntityIds));

        Assert.Equal("p1", model.Prompt.PromptKey);
        Assert.Equal("v9", model.Prompt.PromptVersion);
        Assert.True(model.Prompt.ResolutionSucceeded);

        Assert.True(model.Memory.MemoryRequested);
        Assert.Equal(3, model.Memory.MemoryItemCount);
        Assert.Contains("memory_items_used=3", model.Memory.ParticipationSummary ?? "");

        Assert.True(model.Reliability.FallbackUsed);
        Assert.Equal("policy-x", model.Reliability.PolicyKey);
        Assert.Equal("primary", model.Reliability.PrimaryProviderKey);
        Assert.Equal("fb", model.Reliability.SelectedProviderKey);

        Assert.Equal("Succeeded", model.Status);
        Assert.Equal("full output", model.OutputSummary);
        Assert.Null(model.SafeErrorSummary);
    }

    [Fact]
    public void BuildDenied_UsesSecurityReasonCodes()
    {
        var request = new ExecutionRequest(
            Guid.NewGuid(),
            "c",
            "p",
            new Dictionary<string, string>(),
            new List<string>());

        var m = ExecutionVisibilityReadModelBuilder.BuildDenied(
            Guid.NewGuid(),
            request,
            DateTime.UtcNow,
            DateTime.UtcNow,
            "no access",
            memoryRequested: false);

        Assert.Equal("Denied", m.Status);
        Assert.Contains("AIL.SECURITY.DENIED", m.Explanation.ReasonCodes ?? Array.Empty<string>());
        Assert.False(m.Prompt.ResolutionSucceeded);
    }

    [Fact]
    public void InMemoryExecutionVisibilityReadStore_PutThenTryGet()
    {
        var store = new InMemoryExecutionVisibilityReadStore();
        var id = Guid.NewGuid();
        var request = new ExecutionRequest(
            Guid.NewGuid(),
            "c",
            "p",
            new Dictionary<string, string>(),
            new List<string>(),
            ExecutionInstanceId: id);

        var model = ExecutionVisibilityReadModelBuilder.BuildDenied(
            id,
            request,
            DateTime.UtcNow,
            DateTime.UtcNow,
            "x",
            false);

        store.Put(model);
        var got = store.TryGet(id);
        Assert.NotNull(got);
        Assert.Equal("c", got!.CapabilityKey);
        Assert.Equal("Denied", got.Status);
        Assert.Null(store.TryGet(Guid.NewGuid()));
    }
}
