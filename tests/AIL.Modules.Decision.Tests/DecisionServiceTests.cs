using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using AIL.Modules.Decision.Infrastructure.Strategies;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.MemoryCore.Domain;
using AIL.Modules.Observability.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using KnownSummaries = AIL.Modules.Decision.Domain.KnownMemoryInfluenceSummaries;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionServiceTests
{
    private static IDecisionService CreateService(
        Mock<IMemoryService>? memory = null,
        Mock<IDecisionTelemetryService>? telemetry = null,
        Mock<IDecisionPolicyService>? policy = null)
    {
        memory ??= new Mock<IMemoryService>();
        telemetry ??= new Mock<IDecisionTelemetryService>();
        telemetry.Setup(t => t.TrackAsync(It.IsAny<DecisionTelemetry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var wasDefaultPolicy = policy == null;
        policy ??= new Mock<IDecisionPolicyService>();
        if (wasDefaultPolicy)
        {
            policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string decisionType, CancellationToken _) => new DecisionPolicy(decisionType, MaxOptions: 3, MinimumConfidence: DecisionConfidence.Low));
        }

        var services = new ServiceCollection();
        services.AddSingleton(memory.Object);
        services.AddSingleton(telemetry.Object);
        services.AddSingleton(policy.Object);

        // Manually add decision module services without the default policy
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, CandidateMatchDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, ContextEscalatedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, MemoryInformedDecisionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DecisionContinuityStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDecisionStrategy, DefaultSafeDecisionStrategy>());
        services.AddSingleton<IDecisionService, DecisionService>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDecisionService>();
    }

    private static IDecisionService CreateServiceWithoutStrategies(
        Mock<IMemoryService>? memory = null,
        Mock<IDecisionTelemetryService>? telemetry = null,
        Mock<IDecisionPolicyService>? policy = null)
    {
        memory ??= new Mock<IMemoryService>();
        telemetry ??= new Mock<IDecisionTelemetryService>();
        telemetry.Setup(t => t.TrackAsync(It.IsAny<DecisionTelemetry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        policy ??= new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string decisionType, CancellationToken _) => new DecisionPolicy(decisionType, MaxOptions: 3, MinimumConfidence: DecisionConfidence.Low));

        var services = new ServiceCollection();
        services.AddSingleton(memory.Object);
        services.AddSingleton(telemetry.Object);
        services.AddSingleton(policy.Object);
        services.AddSingleton<IDecisionService, DecisionService>();

        return services.BuildServiceProvider().GetRequiredService<IDecisionService>();
    }

    private static DecisionRequest BaseRequest(Action<DecisionRequestBuilder>? configure = null)
    {
        var b = new DecisionRequestBuilder();
        configure?.Invoke(b);
        return b.Build();
    }

    [Fact]
    public async Task DecideAsync_WithoutMemory_Selects_DefaultSafe()
    {
        var memory = new Mock<IMemoryService>();
        var svc = CreateService(memory);

        var result = await svc.DecideAsync(BaseRequest());

        Assert.Equal(KnownDecisionStrategyKeys.DefaultSafe, result.SelectedStrategyKey);
        Assert.Equal("generic.test", result.PolicyKey);
        Assert.False(result.UsedMemory);
        Assert.Equal(0, result.MemoryItemCount);
        Assert.Contains(KnownDecisionStrategyKeys.DefaultSafe, result.ConsideredStrategies);
        memory.Verify(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()), Times.Never);
        memory.Verify(m => m.GetMemoryByKeyAsync(It.IsAny<GetMemoryByKeyRequest>()), Times.Never);
        Assert.Equal(KnownSummaries.NoMemory, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_ExecutionInstanceId_DoesNotAffect_StrategySelection()
    {
        var svc = CreateService();
        var baseReq = BaseRequest();
        var r1 = await svc.DecideAsync(baseReq with { ExecutionInstanceId = Guid.NewGuid() });
        var r2 = await svc.DecideAsync(baseReq with { ExecutionInstanceId = Guid.NewGuid() });
        var r3 = await svc.DecideAsync(baseReq with { ExecutionInstanceId = null });
        Assert.Equal(r1.SelectedStrategyKey, r2.SelectedStrategyKey);
        Assert.Equal(r2.SelectedStrategyKey, r3.SelectedStrategyKey);
    }

    [Fact]
    public async Task DecideAsync_SingleCandidate_Selects_CandidateKey_Deterministically()
    {
        var svc = CreateService();

        var result = await svc.DecideAsync(BaseRequest(b => b.WithCandidates("custom_route")));

        Assert.Equal("custom_route", result.SelectedStrategyKey);
        Assert.Equal("Decision influenced by exact candidate match", result.ReasonSummary);
        Assert.Equal(DecisionConfidence.High, result.Confidence);
        Assert.Equal(KnownSummaries.NoMemory, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_Escalation_Selects_ContextEscalated()
    {
        var svc = CreateService();

        var result = await svc.DecideAsync(BaseRequest(b =>
            b.WithInputs(new Dictionary<string, string> { ["escalation"] = "true" })));

        Assert.Equal(KnownDecisionStrategyKeys.ContextEscalated, result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.High, result.Confidence);
    }

    [Fact]
    public async Task DecideAsync_PriorityHigh_Selects_ContextEscalated()
    {
        var svc = CreateService();

        var result = await svc.DecideAsync(BaseRequest(b =>
            b.WithInputs(new Dictionary<string, string> { ["Priority"] = "HIGH" })));

        Assert.Equal(KnownDecisionStrategyKeys.ContextEscalated, result.SelectedStrategyKey);
    }

    [Fact]
    public async Task DecideAsync_CandidateWins_Over_Escalation()
    {
        var svc = CreateService();

        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.WithCandidates("only");
            b.WithInputs(new Dictionary<string, string> { ["escalation"] = "true" });
        }));

        Assert.Equal("only", result.SelectedStrategyKey);
    }

    [Fact]
    public async Task DecideAsync_MemoryInformed_When_Memory_And_ContextSignal()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) => new MemoryListResult(
                Items: new[] { CreateMemoryRecord(tenant) },
                PageNumber: 1,
                PageSize: 20,
                TotalCount: 1));

        var svc = CreateService(memory);

        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.WithInputs(new Dictionary<string, string> { ["context_sensitive"] = "true" });
        }));

        Assert.Equal(KnownDecisionStrategyKeys.MemoryInformed, result.SelectedStrategyKey);
        Assert.True(result.UsedMemory);
        Assert.Equal(1, result.MemoryItemCount);
        Assert.Equal(KnownSummaries.MemoryReinforced, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_MemoryInformed_When_Memory_And_ContextText()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) => new MemoryListResult(
                new[] { CreateMemoryRecord(tenant) },
                1,
                20,
                1));

        var svc = CreateService(memory);

        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.ContextText = "context packet";
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
        }));

        Assert.Equal(KnownDecisionStrategyKeys.MemoryInformed, result.SelectedStrategyKey);
        Assert.Equal(KnownSummaries.MemoryReinforced, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_MissingMemory_DoesNotFail_Selects_DefaultSafe()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(Array.Empty<MemoryRecord>(), 1, 20, 0));

        var svc = CreateService(memory);

        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
        }));

        Assert.Equal(KnownDecisionStrategyKeys.DefaultSafe, result.SelectedStrategyKey);
        Assert.True(result.UsedMemory);
        Assert.Equal(0, result.MemoryItemCount);
        Assert.Equal(KnownSummaries.MemoryEmpty, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_ListMemory_Uses_Tenant_From_Request()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) =>
            {
                Assert.Equal(tenant, r.TenantId);
                return new MemoryListResult(Array.Empty<MemoryRecord>(), r.PageNumber, r.PageSize, 0);
            });

        var svc = CreateService(memory);

        _ = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, null);
        }));

        memory.Verify(m => m.ListMemoryAsync(It.Is<ListMemoryRequest>(x => x.TenantId == tenant)), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_WhenEscalation_Both_Escalated_And_Default_Considered()
    {
        var svc = CreateService();
        var result = await svc.DecideAsync(BaseRequest(b =>
            b.WithInputs(new Dictionary<string, string> { ["escalation"] = "true" })));

        Assert.Contains(KnownDecisionStrategyKeys.ContextEscalated, result.ConsideredStrategies);
        Assert.Contains(KnownDecisionStrategyKeys.DefaultSafe, result.ConsideredStrategies);
        Assert.Equal(2, result.ConsideredStrategies.Count);
    }

    [Fact]
    public async Task DecideAsync_Stable_Order_Same_Inputs_Repeated()
    {
        var svc = CreateService();
        var r1 = await svc.DecideAsync(BaseRequest(b => b.WithCandidates("route-a")));
        var r2 = await svc.DecideAsync(BaseRequest(b => b.WithCandidates("route-a")));
        Assert.Equal(r1.SelectedStrategyKey, r2.SelectedStrategyKey);
        Assert.Equal(r1.Confidence, r2.Confidence);
        Assert.Equal(r1.ReasonSummary, r2.ReasonSummary);
    }

    [Fact]
    public async Task DecideAsync_Returns_Options_With_Deterministic_Ordering()
    {
        var svc = CreateService();
        var result = await svc.DecideAsync(BaseRequest(b => b.WithCandidates("route-a")));

        Assert.NotEmpty(result.Options);
        Assert.Equal(result.SelectedStrategyKey, result.Options[0].OptionId);
        Assert.All(result.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.RationaleSummary)));
    }

    [Fact]
    public async Task DecideAsync_Policy_MaxOptions_Shapes_Output()
    {
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("test", MaxOptions: 1, MinimumConfidence: DecisionConfidence.Low));

        var svc = CreateService(policy: policy);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.WithCandidates("route-a");
            b.WithInputs(new Dictionary<string, string> { ["escalation"] = "true" });
        }));

        Assert.Single(result.Options);
    }

    [Fact]
    public async Task DecideAsync_Policy_MinimumConfidence_Filters_Low_Confidence_Options()
    {
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("test", MaxOptions: 3, MinimumConfidence: DecisionConfidence.Medium));

        var svc = CreateService(policy: policy);
        var result = await svc.DecideAsync(BaseRequest(b => b.WithCandidates("route-a")));

        Assert.DoesNotContain(result.Options, option => option.Confidence == DecisionConfidence.Low);
        Assert.Equal("route-a", result.Options[0].OptionId);
    }

    [Fact]
    public async Task DecideAsync_Winner_Can_Be_Below_Policy_MinimumConfidence_With_WinnerFallbackOptions()
    {
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("test", MaxOptions: 3, MinimumConfidence: DecisionConfidence.Medium));

        var svc = CreateService(policy: policy);
        var result = await svc.DecideAsync(BaseRequest());

        Assert.Equal(KnownDecisionStrategyKeys.DefaultSafe, result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.Low, result.Confidence);
        var opt = Assert.Single(result.Options);
        Assert.Equal(KnownDecisionStrategyKeys.DefaultSafe, opt.OptionId);
        Assert.Equal(DecisionConfidence.Low, opt.Confidence);
    }

    [Fact]
    public async Task DecideAsync_Policy_Does_Not_Veto_Winner_Only_Filters_Options_Before_Fallback()
    {
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("strict", MaxOptions: 3, MinimumConfidence: DecisionConfidence.High));

        var svc = CreateService(policy: policy);
        var result = await svc.DecideAsync(BaseRequest());

        Assert.Equal(KnownDecisionStrategyKeys.DefaultSafe, result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.Low, result.Confidence);
        var opt = Assert.Single(result.Options);
        Assert.Equal(result.SelectedStrategyKey, opt.OptionId);
    }

    [Fact]
    public async Task DecideAsync_WinnerFallbackOptions_Are_Deterministic_Across_Repeated_Calls()
    {
        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("test", MaxOptions: 3, MinimumConfidence: DecisionConfidence.Medium));

        var svc = CreateService(policy: policy);
        var req = BaseRequest();
        var r1 = await svc.DecideAsync(req);
        var r2 = await svc.DecideAsync(req);

        Assert.Equal(r1.SelectedStrategyKey, r2.SelectedStrategyKey);
        Assert.Equal(r1.Confidence, r2.Confidence);
        Assert.Equal(r1.ReasonSummary, r2.ReasonSummary);
        Assert.Equal(r1.Options.Count, r2.Options.Count);
        Assert.Equal(r1.Options[0].OptionId, r2.Options[0].OptionId);
        Assert.Equal(r1.Options[0].Confidence, r2.Options[0].Confidence);
        Assert.Equal(r1.Options[0].Strength, r2.Options[0].Strength);
        Assert.Equal(r1.Options[0].RationaleSummary, r2.Options[0].RationaleSummary);
    }

    [Fact]
    public async Task DecideAsync_MemoryInformed_Does_Not_Override_CandidateMatch_Score()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(
                new[] { CreateMemoryRecord(tenant) },
                1,
                20,
                1));

        var policy = new Mock<IDecisionPolicyService>();
        policy.Setup(p => p.ResolvePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new DecisionPolicy("test", MaxOptions: 3, MinimumConfidence: DecisionConfidence.Low));

        var svc = CreateService(memory, policy: policy);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.WithCandidates("route_from_candidate");
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.WithInputs(new Dictionary<string, string> { ["context_sensitive"] = "true" });
        }));

        Assert.Equal("route_from_candidate", result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.High, result.Confidence);
        Assert.Equal(KnownSummaries.MemoryNeutral, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_MemoryInformed_Does_Not_Override_ContextEscalated_Score()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(
                new[] { CreateMemoryRecord(tenant) },
                1,
                20,
                1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.WithInputs(new Dictionary<string, string>
            {
                ["escalation"] = "true",
                ["context_sensitive"] = "true",
            });
        }));

        Assert.Equal(KnownDecisionStrategyKeys.ContextEscalated, result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.High, result.Confidence);
        Assert.Equal(KnownSummaries.MemoryNeutral, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_Continuity_Remains_Weak_Does_Not_Override_CandidateMatch()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(
                new[]
                {
                    CreateContinuityMemoryRecord(tenant, "generic.test", "sole_candidate"),
                },
                1,
                20,
                1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.DecisionType = "generic.test";
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.WithCandidates("sole_candidate");
        }));

        Assert.Equal("sole_candidate", result.SelectedStrategyKey);
        Assert.Equal(DecisionConfidence.High, result.Confidence);
        Assert.Contains(KnownDecisionStrategyKeys.DecisionContinuity, result.ConsideredStrategies);
        Assert.Equal(KnownSummaries.MemoryNeutral, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_IncludeMemoryFalse_DoesNot_Call_Memory()
    {
        var memory = new Mock<IMemoryService>();
        var svc = CreateService(memory);
        _ = await svc.DecideAsync(BaseRequest());
        memory.Verify(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()), Times.Never);
        memory.Verify(m => m.GetMemoryByKeyAsync(It.IsAny<GetMemoryByKeyRequest>()), Times.Never);
    }

    [Fact]
    public async Task DecideAsync_EmptyTenant_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.DecideAsync(BaseRequest(b => b.TenantId = Guid.Empty)));
    }

    [Fact]
    public async Task DecideAsync_IncludeMemory_Without_Query_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.DecideAsync(BaseRequest(b =>
            {
                b.IncludeMemory = true;
                b.MemoryQuery = null;
            })));
    }

    [Fact]
    public async Task DecideAsync_Success_Emits_Consistent_Bounded_Stages()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();

        var svc = CreateService(telemetry: telemetry);

        _ = await svc.DecideAsync(BaseRequest());

        var captured = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => (DecisionTelemetry)i.Arguments[0])
            .ToList();
        var stages = captured.Select(t => t.ExecutionStage).ToList();
        Assert.Equal(new[]
        {
            "EvaluationStarted",
            "StrategiesEvaluated",
            "WinnerSelected",
            "PolicyFiltered",
            "Completed",
        }, stages);
        Assert.All(captured, t => Assert.Null(t.ErrorMessage));
        Assert.DoesNotContain(captured, t => t.ExecutionStage == "Failed");
    }

    [Fact]
    public async Task DecideAsync_Success_Stages_Are_StrictlyIncreasing_And_Not_Duplicated()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateService(telemetry: telemetry);

        _ = await svc.DecideAsync(BaseRequest());

        var stages = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => ((DecisionTelemetry)i.Arguments[0]).ExecutionStage)
            .ToList();
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
    public async Task DecideAsync_Failure_Emits_FailedStage_With_ValidationCategory()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateService(telemetry: telemetry);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.DecideAsync(BaseRequest(b => b.TenantId = Guid.Empty)));

        var captured = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => (DecisionTelemetry)i.Arguments[0])
            .ToList();
        var failed = Assert.Single(captured);
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

        var stages = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => ((DecisionTelemetry)i.Arguments[0]).ExecutionStage)
            .ToList();
        Assert.Equal(new[] { "EvaluationStarted", "Failed" }, stages);

        var failedTelemetry = (DecisionTelemetry)telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Last().Arguments[0];
        Assert.Equal("PolicyResolution", failedTelemetry.FailureCategory);
    }

    [Fact]
    public async Task DecideAsync_NoApplicableStrategies_MapsFailureCategory_To_StrategyEvaluation()
    {
        var telemetry = new Mock<IDecisionTelemetryService>();
        var svc = CreateServiceWithoutStrategies(telemetry: telemetry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DecideAsync(BaseRequest()));

        var emissions = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => (DecisionTelemetry)i.Arguments[0])
            .ToList();
        var failed = Assert.Single(emissions, e => e.ExecutionStage == "Failed");
        Assert.Equal("StrategyEvaluation", failed.FailureCategory);
        Assert.Null(failed.ErrorMessage);
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

        var stages = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => ((DecisionTelemetry)i.Arguments[0]).ExecutionStage)
            .ToList();
        Assert.Equal(new[]
        {
            "EvaluationStarted",
            "StrategiesEvaluated",
            "WinnerSelected",
            "PolicyFiltered",
            "FallbackApplied",
            "Completed",
        }, stages);
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

        var captured = telemetry.Invocations
            .Where(i => i.Method.Name == nameof(IDecisionTelemetryService.TrackAsync))
            .Select(i => (DecisionTelemetry)i.Arguments[0])
            .ToList();
        Assert.NotEmpty(captured);
        Assert.All(captured, t =>
        {
            Assert.DoesNotContain(secret, t.DecisionType, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.SelectedStrategyKey, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.PolicyKey ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.MemoryInfluenceSummary ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.ExecutionStage, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.ConfidenceTier ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.FailureCategory ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, t.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DecideAsync_MemoryInfluenceSummary_MemoryConflict_WhenContinuityAndMemoryInformedDisagree()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(
                new[] { CreateContinuityMemoryRecord(tenant, "generic.test", "continuity_route") },
                1,
                20,
                1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.DecisionType = "generic.test";
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.ContextText = "operator context";
            b.Candidates = new[] { "continuity_route", "memory_informed", "default_safe" };
        }));

        Assert.Contains(KnownDecisionStrategyKeys.MemoryInformed, result.ConsideredStrategies);
        Assert.Contains(KnownDecisionStrategyKeys.DecisionContinuity, result.ConsideredStrategies);
        Assert.Equal(KnownSummaries.MemoryConflict, result.MemoryInfluenceSummary);
        Assert.Equal(KnownDecisionStrategyKeys.MemoryInformed, result.SelectedStrategyKey);
    }

    [Fact]
    public async Task DecideAsync_MemoryInformedOptionStrength_ScalesDeterministicallyWithBoundedItemCount()
    {
        var tenant = Guid.NewGuid();
        async Task<DecisionResult> RunAsync(int count)
        {
            var records = Enumerable.Range(0, count).Select(_ => CreateMemoryRecord(tenant)).ToArray();
            var memory = new Mock<IMemoryService>();
            memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
                .ReturnsAsync(new MemoryListResult(records, 1, 20, count));
            var svc = CreateService(memory);
            return await svc.DecideAsync(BaseRequest(b =>
            {
                b.TenantId = tenant;
                b.IncludeMemory = true;
                b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
                b.WithInputs(new Dictionary<string, string> { ["context_sensitive"] = "true" });
            }));
        }

        var one = await RunAsync(1);
        var many = await RunAsync(12);
        var optOne = Assert.Single(one.Options, o => o.OptionId == KnownDecisionStrategyKeys.MemoryInformed);
        var optMany = Assert.Single(many.Options, o => o.OptionId == KnownDecisionStrategyKeys.MemoryInformed);
        Assert.True(optMany.Strength > optOne.Strength);
    }

    private static MemoryRecord CreateMemoryRecord(Guid tenant) =>
        MemoryRecord.Create(
            Guid.NewGuid(),
            tenant,
            MemoryScopeType.Tenant,
            null,
            MemoryKind.Fact,
            "k",
            "body",
            null,
            MemoryImportance.Low,
            MemorySource.UserInput,
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static MemoryRecord CreateContinuityMemoryRecord(Guid tenant, string decisionType, string selectedStrategy) =>
        MemoryRecord.Create(
            Guid.NewGuid(),
            tenant,
            MemoryScopeType.Tenant,
            null,
            MemoryKind.Fact,
            "continuity_key",
            selectedStrategy,
            new Dictionary<string, string> { ["decision_type"] = decisionType },
            MemoryImportance.Low,
            MemorySource.UserInput,
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static MemoryRecord CreateContinuityMemoryRecord(Guid tenant, string decisionType, string selectedStrategy, string otherRoute) =>
        MemoryRecord.Create(
            Guid.NewGuid(),
            tenant,
            MemoryScopeType.Tenant,
            null,
            MemoryKind.Fact,
            "continuity_key",
            selectedStrategy,
            new Dictionary<string, string> { ["decision_type"] = decisionType, ["other_candidate"] = otherRoute },
            MemoryImportance.Low,
            MemorySource.UserInput,
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static int GetStrategyScore(DecisionResult result, string strategyKey)
    {
        // Helper to extract score from considered strategies - not directly available, so approximate
        // In real test, might need to mock or inspect internals, but for now assume continuity wins if selected
        return result.ReasonSummary.Contains("continuity") ? 100 : 0;
    }

    private sealed class DecisionRequestBuilder
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();
        public string DecisionType { get; set; } = "generic.test";
        public string SubjectType { get; set; } = "subject";
        public string SubjectId { get; set; } = "id-1";
        public string? ContextText { get; set; }
        public IReadOnlyDictionary<string, string>? Inputs { get; set; }
        public bool IncludeMemory { get; set; }
        public DecisionMemoryQuery? MemoryQuery { get; set; }
        public IReadOnlyList<string>? Candidates { get; set; }

        public DecisionRequestBuilder WithInputs(IReadOnlyDictionary<string, string> d)
        {
            Inputs = d;
            return this;
        }

        public DecisionRequestBuilder WithCandidates(string single)
        {
            Candidates = new[] { single };
            return this;
        }

        public DecisionRequestBuilder WithCandidates(params string[] candidates)
        {
            Candidates = candidates;
            return this;
        }

        public DecisionRequest Build() =>
            new(
                TenantId,
                DecisionType,
                SubjectType,
                SubjectId,
                ContextText,
                Inputs,
                IncludeMemory,
                MemoryQuery,
                Candidates,
                Metadata: null,
                CorrelationGroupId: null,
                ExecutionInstanceId: null);
    }

    [Fact]
    public async Task DecideAsync_Continuity_Applies_When_Qualifying_Memory_Exists()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        
        // Create memory record with decision_type metadata matching the request
        var continuityRecord = MemoryRecord.Create(
            Guid.NewGuid(),
            tenant,
            MemoryScopeType.Tenant,
            null,
            MemoryKind.Fact,
            "continuity_key",
            "memory_informed",
            new Dictionary<string, string> { ["decision_type"] = "generic.test" },
            MemoryImportance.Low,
            MemorySource.UserInput,
            DateTime.UtcNow,
            DateTime.UtcNow);
            
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync(new MemoryListResult(
                Items: new[] { continuityRecord },
                PageNumber: 1,
                PageSize: 20,
                TotalCount: 1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.DecisionType = "generic.test";
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            // Set candidates that include the historical strategy
            b.Candidates = new[] { "memory_informed", "default_safe" };
        }));

        // Verify memory was used
        Assert.True(result.UsedMemory);
        Assert.Equal(1, result.MemoryItemCount);
        
        // Verify continuity strategy was considered
        Assert.Contains("decision_continuity", result.ConsideredStrategies);
        Assert.Equal(KnownSummaries.MemoryNeutral, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_Continuity_Suggests_Historical_Strategy_When_Available()
    {
        var tenant = Guid.NewGuid();
        var historicalStrategy = "context_escalated";
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) => new MemoryListResult(
                Items: new[] { CreateContinuityMemoryRecord(tenant, "generic.test", historicalStrategy) },
                PageNumber: 1,
                PageSize: 20,
                TotalCount: 1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            // Provide multiple candidates where one is the historical strategy
            b.Candidates = new[] { historicalStrategy, "default_safe", "memory_informed" };
        }));

        // Should have loaded memory and considered continuity
        Assert.True(result.UsedMemory);
        Assert.Equal(1, result.MemoryItemCount);
        Assert.Contains(KnownDecisionStrategyKeys.DecisionContinuity, result.ConsideredStrategies);
        Assert.Equal(historicalStrategy, result.SelectedStrategyKey);
        Assert.Equal(KnownSummaries.MemoryConsistent, result.MemoryInfluenceSummary);
    }

    [Fact]
    public async Task DecideAsync_Continuity_NoOp_When_No_Qualifying_History()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) => new MemoryListResult(
                Items: new[] { CreateMemoryRecord(tenant) }, // No continuity metadata (no decision_type)
                PageNumber: 1,
                PageSize: 20,
                TotalCount: 1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.Candidates = new[] { "memory_informed", "default_safe" };
        }));

        // When memory doesn't have decision_type, continuity strategy won't apply
        Assert.True(result.UsedMemory);
        Assert.Equal(1, result.MemoryItemCount);
        Assert.DoesNotContain("decision_continuity", result.ReasonSummary);
    }

    [Fact]
    public async Task DecideAsync_Continuity_Requires_Matching_DecisionType()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.ListMemoryAsync(It.IsAny<ListMemoryRequest>()))
            .ReturnsAsync((ListMemoryRequest r) => new MemoryListResult(
                Items: new[] { CreateContinuityMemoryRecord(tenant, "different.type", "memory_informed") },
                PageNumber: 1,
                PageSize: 20,
                TotalCount: 1));

        var svc = CreateService(memory);
        var result = await svc.DecideAsync(BaseRequest(b =>
        {
            b.TenantId = tenant;
            b.DecisionType = "generic.test"; // Different from memory's "different.type"
            b.IncludeMemory = true;
            b.MemoryQuery = new DecisionMemoryQuery("Tenant", null, "Fact");
            b.Candidates = new[] { "memory_informed", "default_safe" };
        }));

        // When decision_type doesn't match, continuity strategy won't find qualifying history
        Assert.True(result.UsedMemory);
        Assert.Equal(1, result.MemoryItemCount);
        Assert.DoesNotContain("continuity", result.ReasonSummary.ToLowerInvariant());
    }

    [Fact]
    public void DecisionMemoryLoader_InvalidTakeRecent_Throws()
    {
        var memory = new Mock<IMemoryService>();
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionMemoryLoader.LoadAsync(
                memory.Object,
                Guid.NewGuid(),
                new DecisionMemoryQuery("Tenant", null, null, TakeRecent: 0),
                CancellationToken.None).GetAwaiter().GetResult());
    }
}

public sealed class DecisionConfidenceMapperTests
{
    // REGRESSION TESTS: Existing score-to-confidence mappings must remain unchanged

    [Fact]
    public void FromScore_DefaultSafeScore_Maps_To_Low()
    {
        // DefaultSafeDecisionStrategy returns 100
        var confidence = DecisionConfidenceMapper.FromScore(100);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_ContinuityScore_Maps_To_Low()
    {
        // DecisionContinuityStrategy returns 100
        var confidence = DecisionConfidenceMapper.FromScore(100);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_MemoryInformedScore_Maps_To_Medium()
    {
        // MemoryInformedDecisionStrategy returns 800
        var confidence = DecisionConfidenceMapper.FromScore(800);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    [Fact]
    public void FromScore_ContextEscalatedScore_Maps_To_High()
    {
        // ContextEscalatedDecisionStrategy returns 900
        var confidence = DecisionConfidenceMapper.FromScore(900);
        Assert.Equal(DecisionConfidence.High, confidence);
    }

    [Fact]
    public void FromScore_CandidateMatchScore_Maps_To_High()
    {
        // CandidateMatchDecisionStrategy returns 1000
        var confidence = DecisionConfidenceMapper.FromScore(1000);
        Assert.Equal(DecisionConfidence.High, confidence);
    }

    // BOUNDARY TESTS: Ensure threshold boundaries are precise and deterministic

    [Fact]
    public void FromScore_LowConfidenceUpperBound_Maps_To_Low()
    {
        // Just below Medium threshold (500)
        var confidence = DecisionConfidenceMapper.FromScore(499);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_MediumConfidenceLowerBound_Maps_To_Medium()
    {
        // Exactly at Medium threshold (500)
        var confidence = DecisionConfidenceMapper.FromScore(500);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    [Fact]
    public void FromScore_MediumConfidenceUpperBound_Maps_To_Medium()
    {
        // Just below High threshold (900)
        var confidence = DecisionConfidenceMapper.FromScore(899);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    [Fact]
    public void FromScore_HighConfidenceLowerBound_Maps_To_High()
    {
        // Exactly at High threshold (900)
        var confidence = DecisionConfidenceMapper.FromScore(900);
        Assert.Equal(DecisionConfidence.High, confidence);
    }

    // OUT-OF-RANGE TESTS: Ensure invalid scores are normalized safely

    [Fact]
    public void FromScore_NegativeScore_Clamped_To_Low()
    {
        // Negative score should be clamped to 0 → Low
        var confidence = DecisionConfidenceMapper.FromScore(-100);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_ExcessiveScore_Clamped_To_High()
    {
        // Score > 1000 should be clamped to 1000 → High
        var confidence = DecisionConfidenceMapper.FromScore(2000);
        Assert.Equal(DecisionConfidence.High, confidence);
    }

    [Fact]
    public void FromScore_ZeroScore_Maps_To_Low()
    {
        // Score 0 is valid and maps to Low
        var confidence = DecisionConfidenceMapper.FromScore(0);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_MaxScore_Maps_To_High()
    {
        // Score 1000 is maximum and maps to High
        var confidence = DecisionConfidenceMapper.FromScore(1000);
        Assert.Equal(DecisionConfidence.High, confidence);
    }

    // DETERMINISM TESTS: Same input always produces same output

    [Fact]
    public void FromScore_Deterministic_Multiple_Calls()
    {
        var score = 650;
        var result1 = DecisionConfidenceMapper.FromScore(score);
        var result2 = DecisionConfidenceMapper.FromScore(score);
        var result3 = DecisionConfidenceMapper.FromScore(score);

        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    // WEAK SIGNAL SAFETY: Low scores remain conservatively Low

    [Fact]
    public void FromScore_WeakSignal_Remains_Low()
    {
        // Even a score of 400 (well-established continuity/default)
        // should not inflate to Medium
        var confidence = DecisionConfidenceMapper.FromScore(400);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_ModerateScore_Maps_To_Medium()
    {
        // Score in the clear Medium range
        var confidence = DecisionConfidenceMapper.FromScore(700);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    // EDGE CASE TESTS: Boundary-adjacent values

    [Fact]
    public void FromScore_OneBeforeMedianThreshold_Maps_To_Low()
    {
        var confidence = DecisionConfidenceMapper.FromScore(499);
        Assert.Equal(DecisionConfidence.Low, confidence);
    }

    [Fact]
    public void FromScore_OneAfterMedianThreshold_Maps_To_Medium()
    {
        var confidence = DecisionConfidenceMapper.FromScore(501);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    [Fact]
    public void FromScore_OneBeforeHighThreshold_Maps_To_Medium()
    {
        var confidence = DecisionConfidenceMapper.FromScore(899);
        Assert.Equal(DecisionConfidence.Medium, confidence);
    }

    [Fact]
    public void FromScore_OneAfterHighThreshold_Maps_To_High()
    {
        var confidence = DecisionConfidenceMapper.FromScore(901);
        Assert.Equal(DecisionConfidence.High, confidence);
    }
}

public sealed class DecisionExplanationBuilderTests
{
    [Fact]
    public void BuildExplanation_SingleSignal_Returns_SingleSentence()
    {
        var explanation = DecisionExplanationBuilder.BuildExplanation(new[]
        {
            new DecisionSignal(DecisionSignalType.EscalationSignal, 90, true)
        });

        Assert.Equal("Decision influenced by escalation or priority signal", explanation);
    }

    [Fact]
    public void BuildExplanation_TwoSignals_Uses_Deterministic_Ordering()
    {
        var escalation = new DecisionSignal(DecisionSignalType.EscalationSignal, 90, true);
        var memory = new DecisionSignal(DecisionSignalType.MemoryContext, 70, true);

        var a = DecisionExplanationBuilder.BuildExplanation(new[] { escalation, memory });
        var b = DecisionExplanationBuilder.BuildExplanation(new[] { memory, escalation });

        Assert.Equal(a, b);
        Assert.Equal("Decision informed by escalation or priority signal and memory and contextual information", a);
    }

    [Fact]
    public void BuildExplanation_ThreeSignals_Bounds_To_TopTwo()
    {
        var explanation = DecisionExplanationBuilder.BuildExplanation(new[]
        {
            new DecisionSignal(DecisionSignalType.MemoryContext, 70, true),
            new DecisionSignal(DecisionSignalType.HistoricalContinuity, 80, true),
            new DecisionSignal(DecisionSignalType.DefaultFallback, 10, true)
        });

        Assert.Equal("Decision informed by consistent historical precedent and memory and contextual information", explanation);
        Assert.DoesNotContain("default evaluation", explanation);
    }

    [Fact]
    public void BuildExplanation_UnknownSignal_Returns_Generic_Fallback()
    {
        var explanation = DecisionExplanationBuilder.BuildExplanation(new[]
        {
            new DecisionSignal(DecisionSignalType.Unknown, 50, true)
        });

        Assert.Equal("Decision influenced by evaluated signals", explanation);
    }

    [Fact]
    public void BuildExplanation_NoActiveSignals_Returns_Fallback()
    {
        var explanation = DecisionExplanationBuilder.BuildExplanation(new[]
        {
            new DecisionSignal(DecisionSignalType.CandidateMatch, 100, false)
        });

        Assert.Equal("Decision based on standard evaluation", explanation);
    }
}

