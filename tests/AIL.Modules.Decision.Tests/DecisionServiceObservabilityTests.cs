using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.Observability.Application;
using Moq;

namespace AIL.Modules.Decision.Tests;

public sealed partial class DecisionServiceTests
{
    // Observability contract helper: extract emitted telemetry records in call order.
    private static List<DecisionTelemetry> CapturedTelemetry(Mock<IDecisionTelemetryService> telemetry) =>
        telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => (DecisionTelemetry)i.Arguments[0])
            .ToList();

    // Observability contract helper: assert a secret token does not appear in bounded telemetry fields.
    private static void AssertNoSensitiveToken(IEnumerable<DecisionTelemetry> emissions, string token)
    {
        Assert.All(emissions, e =>
        {
            Assert.DoesNotContain(token, e.DecisionType, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.SelectedStrategyKey, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.PolicyKey ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.MemoryInfluenceSummary ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.ExecutionStage, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.ConfidenceTier ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.FailureCategory ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(token, e.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DecideAsync_Success_Emits_Consistent_Bounded_Stages()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateService(telemetry: telemetry);

        _ = await svc.DecideAsync(BaseRequest());

        var captured = CapturedTelemetry(telemetry);
        Assert.Equal(new[]
        {
            "EvaluationStarted",
            "StrategiesEvaluated",
            "WinnerSelected",
            "PolicyFiltered",
            "Completed",
        }, captured.Select(t => t.ExecutionStage).ToList());
        Assert.All(captured, t => Assert.Null(t.ErrorMessage));
        Assert.DoesNotContain(captured, t => t.ExecutionStage == "Failed");
    }

    [Fact]
    public async Task DecideAsync_Success_Stages_Are_StrictlyIncreasing_And_Not_Duplicated()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateService(telemetry: telemetry);
        _ = await svc.DecideAsync(BaseRequest());

        var stages = CapturedTelemetry(telemetry).Select(t => t.ExecutionStage).ToList();
        Assert.Equal(stages.Count, stages.Distinct(StringComparer.Ordinal).Count());

        var index = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["EvaluationStarted"] = 0,
            ["StrategiesEvaluated"] = 1,
            ["WinnerSelected"] = 2,
            ["PolicyFiltered"] = 3,
            ["FallbackApplied"] = 4,
            ["Completed"] = 5,
        };
        var numeric = stages.Select(s => index[s]).ToList();
        for (var i = 1; i < numeric.Count; i++)
            Assert.True(numeric[i] > numeric[i - 1], "Stage order must be strictly increasing.");
    }

    [Fact]
    public async Task DecideAsync_FallbackApplied_Emits_Optional_Stage_Between_Filtered_And_Completed()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionPolicy("strict", MaxOptions: 3, MinimumConfidence: DecisionConfidence.High));
        var svc = CreateService(telemetry: telemetry, policy: policy);

        _ = await svc.DecideAsync(BaseRequest());

        Assert.Equal(new[]
        {
            "EvaluationStarted",
            "StrategiesEvaluated",
            "WinnerSelected",
            "PolicyFiltered",
            "FallbackApplied",
            "Completed",
        }, CapturedTelemetry(telemetry).Select(t => t.ExecutionStage).ToList());
    }

    [Fact]
    public async Task DecideAsync_Failure_Emits_FailedStage_With_ValidationCategory()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateService(telemetry: telemetry);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.DecideAsync(BaseRequest(b => b.TenantId = Guid.Empty)));

        var failed = Assert.Single(CapturedTelemetry(telemetry));
        Assert.Equal("Failed", failed.ExecutionStage);
        Assert.Equal("Validation", failed.FailureCategory);
        Assert.Null(failed.ErrorMessage);
    }

    [Fact]
    public async Task DecideAsync_PolicyResolutionFailure_Ends_With_Failed_And_No_Stages_After()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("policy unavailable"));
        var svc = CreateService(telemetry: telemetry, policy: policy);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DecideAsync(BaseRequest()));

        var emissions = CapturedTelemetry(telemetry);
        Assert.Equal(new[] { "EvaluationStarted", "Failed" }, emissions.Select(e => e.ExecutionStage).ToList());
        Assert.Equal("PolicyResolution", emissions[^1].FailureCategory);
    }

    [Fact]
    public async Task DecideAsync_NoApplicableStrategies_MapsFailureCategory_To_StrategyEvaluation()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateServiceWithoutStrategies(telemetry: telemetry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DecideAsync(BaseRequest()));

        var failed = Assert.Single(CapturedTelemetry(telemetry), e => e.ExecutionStage == "Failed");
        Assert.Equal("StrategyEvaluation", failed.FailureCategory);
        Assert.Null(failed.ErrorMessage);
    }

    [Fact]
    public async Task DecideAsync_Cancellation_MapsFailureCategory_To_Canceled_And_NotUnexpected()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("caller canceled"));
        var svc = CreateService(telemetry: telemetry, policy: policy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.DecideAsync(BaseRequest()));

        var emissions = CapturedTelemetry(telemetry);
        Assert.Equal(new[] { "EvaluationStarted", "Failed" }, emissions.Select(e => e.ExecutionStage).ToList());
        var failed = Assert.Single(emissions, e => e.ExecutionStage == "Failed");
        Assert.Equal("Canceled", failed.FailureCategory);
        Assert.NotEqual("Unexpected", failed.FailureCategory);
        Assert.Null(failed.ErrorMessage);
    }

    [Fact]
    public async Task DecideAsync_CancellationTelemetry_DoesNotContain_ExceptionMessageText()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var policy = new Mock<IDecisionPolicyService>();
        const string secretCancellationMessage = "SECRET_CANCEL_TOKEN_ABC123";
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException(secretCancellationMessage));
        var svc = CreateService(telemetry: telemetry, policy: policy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.DecideAsync(BaseRequest()));

        var emissions = CapturedTelemetry(telemetry);
        Assert.NotEmpty(emissions);
        AssertNoSensitiveToken(emissions, secretCancellationMessage);
    }

    [Fact]
    public async Task DecideAsync_Telemetry_Does_Not_Leak_Context_Or_Memory_Content()
    {
        var secret = "TOP_SECRET_CONTEXT_TOKEN_123";
        var telemetry = new Mock<IDecisionTelemetryService>();

        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(new[] { CreateMemoryRecord(tenant) }, 1, 20, 1));
        var svc = CreateService(memory, telemetry: telemetry);

        _ = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.ContextText = secret;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
        }));

        var captured = CapturedTelemetry(telemetry);
        Assert.NotEmpty(captured);
        AssertNoSensitiveToken(captured, secret);
    }
}
