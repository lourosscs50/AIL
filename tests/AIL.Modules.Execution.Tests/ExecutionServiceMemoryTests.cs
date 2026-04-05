using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using AIL.Modules.ContextEngine.Application;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Application.Visibility;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.Observability.Application;
using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.PolicyRegistry.Domain;
using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using AIL.Modules.Security.Application;
using AIL.Modules.Security.Domain;
using Moq;
using System.Text.Json;

namespace AIL.Modules.Execution.Tests;

public sealed class ExecutionServiceMemoryTests
{
    private static ExecutionService CreateService(
        Mock<IMemoryService> memory,
        Mock<IExecutionReliabilityService>? reliability = null,
        Mock<IExecutionTelemetryService>? telemetry = null,
        Dictionary<string, string>? promptVariables = null,
        bool? capabilityDefaultIncludeMemory = null,
        int? capabilityDefaultMemoryMaxResults = null,
        IEnumerable<IExecutionMemoryStrategy>? strategies = null)
    {
        var security = new Mock<ISecurityService>();
        security
            .Setup(s => s.EvaluateAccessAsync(It.IsAny<TenantId?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionAccessDecision.Allow());

        var variableDefs = promptVariables?.ToDictionary(
            kv => kv.Key,
            kv => new PromptVariableDefinition(kv.Key, Required: true),
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, PromptVariableDefinition>(StringComparer.OrdinalIgnoreCase);

        var promptRegistry = new Mock<IPromptRegistryService>();
        promptRegistry
            .Setup(p => p.ResolvePromptAsync("p", null, It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptResolution("p", "v1", "hello", variableDefs));

        var policyRegistry = new Mock<IPolicyRegistryService>();
        policyRegistry
            .Setup(p => p.ResolvePolicyAsync("cap", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionPolicy(
                PolicyKey: "policy",
                Description: "",
                PrimaryProviderKey: "stub",
                PrimaryModelKey: "m1",
                FallbackAllowed: false,
                FallbackProviderKey: null,
                FallbackModelKey: null,
                MaxTokens: 100,
                TimeoutMs: 1000,
                DefaultIncludeMemory: capabilityDefaultIncludeMemory,
                DefaultMemoryMaxResults: capabilityDefaultMemoryMaxResults));

        var selection = new Mock<IProviderSelectionService>();
        selection
            .Setup(s => s.SelectAsync("cap", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(
                PrimaryProviderKey: "stub",
                PrimaryModelKey: "m1",
                FallbackProviderKey: null,
                FallbackModelKey: null,
                MaxTokens: 100,
                FallbackAllowed: false,
                TimeoutMs: 1000));

        var contextEngine = new Mock<IContextEngineService>();
        contextEngine
            .Setup(c => c.BuildContextAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIL.Modules.ContextEngine.Domain.ExecutionContext(
                Variables: new Dictionary<string, string> { ["k"] = "v" },
                ReferenceIds: new List<string>()));

        if (reliability is null)
        {
            reliability = new Mock<IExecutionReliabilityService>();
            reliability
                .Setup(r => r.ExecuteWithReliabilityAsync(It.IsAny<ProviderExecutionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderExecutionResult("stub", "m1", "ok", false, null, null));
        }

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.RecordAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        if (telemetry is null)
        {
            telemetry = new Mock<IExecutionTelemetryService>();
            telemetry.Setup(t => t.TrackAsync(It.IsAny<ExecutionTelemetry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        var visibilityStore = new Mock<IExecutionVisibilityReadStore>();
        visibilityStore.Setup(v => v.Put(It.IsAny<ExecutionVisibilityReadModel>()));
        visibilityStore
            .Setup(v => v.ListByCompletedAtDescending(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((Array.Empty<ExecutionVisibilityReadModel>(), 0));

        return new ExecutionService(
            security.Object,
            promptRegistry.Object,
            policyRegistry.Object,
            selection.Object,
            contextEngine.Object,
            memory.Object,
            new MemoryContextAssembler(),
            reliability.Object,
            audit.Object,
            telemetry.Object,
            visibilityStore.Object,
            strategies ?? Array.Empty<IExecutionMemoryStrategy>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMemory_DoesNotCallMemory_AndLeavesContextUnchanged()
    {
        var memory = new Mock<IMemoryService>();
        ProviderExecutionRequest? captured = null;
        var reliability = new Mock<IExecutionReliabilityService>();
        reliability
            .Setup(r => r.ExecuteWithReliabilityAsync(It.IsAny<ProviderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderExecutionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ProviderExecutionResult("stub", "m1", "ok", false, null, null));

        var svc = CreateService(memory, reliability);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>());

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
        Assert.NotNull(captured);
        Assert.StartsWith("refs=[] vars=[k]", captured!.ContextText, StringComparison.Ordinal);
        Assert.DoesNotContain("memory_context=", captured.ContextText, StringComparison.Ordinal);
        Assert.Equal("False", captured.Metadata["MemoryRequested"]);
        Assert.False(captured.Metadata.ContainsKey("MemoryItemCount"));
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeMemory_ThrowsWhenMemoryQueryMissing()
    {
        var memory = new Mock<IMemoryService>();
        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: null);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.ExecuteAsync(request, CancellationToken.None));
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithKeys_UsesRetrieve_PerTenantAndMapsContext()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync((RetrieveMemoryRequest r) =>
                r.TenantId == tenant && r.Key == "alpha"
                    ? new RetrieveMemoryResponse(new[]
                    {
                        new MemoryRecordResponse(
                            Id: Guid.NewGuid(),
                            TenantId: tenant,
                            ScopeType: "Tenant",
                            ScopeId: "",
                            MemoryKind: "Fact",
                            Key: "alpha",
                            Content: "c1",
                            Metadata: new Dictionary<string, string> { ["md"] = "x" },
                            Importance: "Low",
                            Source: "UserInput",
                            CreatedAtUtc: DateTime.UtcNow,
                            UpdatedAtUtc: DateTime.UtcNow)
                    })
                    : new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        ProviderExecutionRequest? captured = null;
        var reliability = new Mock<IExecutionReliabilityService>();
        reliability
            .Setup(r => r.ExecuteWithReliabilityAsync(It.IsAny<ProviderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderExecutionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ProviderExecutionResult("stub", "m1", "ok", false, null, null));

        var svc = CreateService(memory, reliability);

        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery(
                ScopeType: "Tenant",
                ScopeId: null,
                MemoryKind: "Fact",
                Keys: new[] { "alpha" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(x =>
            x.TenantId == tenant && x.Key == "alpha" && x.MemoryKind == "Fact")), Times.Once);
        Assert.NotNull(captured);
        Assert.Contains("memory_context=", captured!.ContextText, StringComparison.Ordinal);
        Assert.Contains("\"content\":\"c1\"", captured.ContextText, StringComparison.Ordinal);
        Assert.Equal("True", captured.Metadata["MemoryRequested"]);
        Assert.Equal("1", captured.Metadata["MemoryItemCount"]);
    }

    [Fact]
    public async Task ExecuteAsync_ListMode_UsesRetrieve_WithDefaultMaxResults()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync((RetrieveMemoryRequest r) =>
            {
                Assert.Equal(tenant, r.TenantId);
                Assert.Equal("Tenant", r.ScopeType);
                Assert.Equal(ExecutionMemoryLoader.DefaultTakeRecent, r.MaxResults);
                return new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>());
            });

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MissingMemory_DoesNotFail_AndInjectsEmptyItems()
    {
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        ProviderExecutionRequest? captured = null;
        var reliability = new Mock<IExecutionReliabilityService>();
        reliability
            .Setup(r => r.ExecuteWithReliabilityAsync(It.IsAny<ProviderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderExecutionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ProviderExecutionResult("stub", "m1", "ok", false, null, null));

        var svc = CreateService(memory, reliability);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "nope" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Contains("memory_context=", captured!.ContextText, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(captured.ContextText.Split("memory_context=", 2)[1]);
        Assert.Equal(0, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("0", captured.Metadata["MemoryItemCount"]);
    }

    [Fact]
    public async Task ExecuteAsync_TakeRecent_IsPassedAsMaxResults()
    {
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync((RetrieveMemoryRequest r) =>
                new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null, TakeRecent: 7));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 7)), Times.Once);
    }

    [Fact]
    public void ExecutionMemoryLoader_RejectsInvalidTakeRecent()
    {
        var memory = new Mock<IMemoryService>();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExecutionMemoryLoader.LoadAsync(
                memory.Object,
                Guid.NewGuid(),
                new ExecutionMemoryQuery("Tenant", null, null, TakeRecent: 0),
                CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("TakeRecent", ex.ParamName);
    }

    [Fact]
    public void ExecutionMemoryLoader_RejectsBlankScopeType()
    {
        var memory = new Mock<IMemoryService>();
        Assert.Throws<ArgumentException>(() =>
            ExecutionMemoryLoader.LoadAsync(
                memory.Object,
                Guid.NewGuid(),
                new ExecutionMemoryQuery(" ", null, null),
                CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task ExecuteAsync_RecordsMemoryItemCount_InTelemetry()
    {
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new[]
            {
                new MemoryRecordResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Tenant",
                    null,
                    "Fact",
                    "k1",
                    "body",
                    new Dictionary<string, string>(),
                    "Low",
                    "UserInput",
                    DateTime.UtcNow,
                    DateTime.UtcNow)
            }));

        var telemetry = new Mock<IExecutionTelemetryService>();
        ExecutionTelemetry? last = null;
        telemetry
            .Setup(t => t.TrackAsync(It.IsAny<ExecutionTelemetry>(), It.IsAny<CancellationToken>()))
            .Callback<ExecutionTelemetry, CancellationToken>((e, _) => last = e)
            .Returns(Task.CompletedTask);

        var svc = CreateService(memory, telemetry: telemetry);

        var tenant = Guid.NewGuid();
        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(last);
        Assert.True(last!.MemoryRequested);
        Assert.Equal(1, last.MemoryItemCount);
    }

    [Fact]
    public async Task ExecuteAsync_ListMemory_UsesExecutionTenantId_ForIsolation()
    {
        var tenantA = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync((RetrieveMemoryRequest r) =>
            {
                Assert.Equal(tenantA, r.TenantId);
                return new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>());
            });

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: tenantA,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.TenantId == tenantA)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_IncludeMetadataFalse_OmitsMetadataInJson()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        memory
            .Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new[]
            {
                new MemoryRecordResponse(
                    Guid.NewGuid(),
                    tenant,
                    "Tenant",
                    "",
                    "Fact",
                    "k",
                    "body",
                    new Dictionary<string, string> { ["secret"] = "x" },
                    "Low",
                    "UserInput",
                    DateTime.UtcNow,
                    DateTime.UtcNow)
            }));

        ProviderExecutionRequest? captured = null;
        var reliability = new Mock<IExecutionReliabilityService>();
        reliability
            .Setup(r => r.ExecuteWithReliabilityAsync(It.IsAny<ProviderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderExecutionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ProviderExecutionResult("stub", "m1", "ok", false, null, null));

        var svc = CreateService(memory, reliability);

        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "k" }, IncludeMetadata: false));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        var json = captured!.ContextText.Split("memory_context=", 2)[1];
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeMemoryFalse_DoesNotRetrieveMemory()
    {
        var memory = new Mock<IMemoryService>();
        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: false);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeMemoryNull_UsesDefaultFalse_DoesNotRetrieveMemory()
    {
        var memory = new Mock<IMemoryService>();
        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMemoryMaxResults_UsesSpecifiedValue()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: 3);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 3)), Times.Once);
    }

    // ===== PHASE 6: CAPABILITY-LEVEL MEMORY DEFAULTS =====

    [Fact]
    public async Task ExecuteAsync_CapabilityDefaultIncludeMemoryTrue_EnablesMemoryWhenRequestOmitsIt()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultIncludeMemory: true);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should call memory retrieval because capability default enables it
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CapabilityDefaultIncludeMemoryFalse_DisablesMemoryWhenRequestOmitsIt()
    {
        var memory = new Mock<IMemoryService>();

        var svc = CreateService(memory, capabilityDefaultIncludeMemory: false);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should not call memory retrieval even with capability default false
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RequestIncludeMemoryTrueOverridesCapabilityFalse()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultIncludeMemory: false);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,  // Request explicitly enables it
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Request override should take precedence
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RequestIncludeMemoryFalseOverridesCapabilityTrue()
    {
        var memory = new Mock<IMemoryService>();

        var svc = CreateService(memory, capabilityDefaultIncludeMemory: true);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: false);  // Request explicitly disables it

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Request override should take precedence
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RequestMemoryMaxResultsOverridesCapabilityDefault()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultMemoryMaxResults: 7);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: 3);  // Request explicitly sets it to 3

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Request override should take precedence over capability default
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 3)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CapabilityDefaultMemoryMaxResults_AppliesWhenRequestOmitsIt()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultMemoryMaxResults: 7);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should use capability default
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 7)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SystemDefaultMemoryMaxResults_AppliesWhenNeitherRequestNorCapabilitySpecifies()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);  // No capability defaults specified

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),  // Kind-based query (not true list mode)
            MemoryMaxResults: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should use system default of 5 for kind-based queries
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 5)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SystemDefaultMemoryMaxResults_ListMode_Uses20WhenNeitherRequestNorCapabilitySpecifies()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);  // No capability defaults specified

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null),  // True list mode (no Kind, no Keys)
            MemoryMaxResults: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should use DefaultTakeRecent (20) for true list mode
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == ExecutionMemoryLoader.DefaultTakeRecent)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MemoryMaxResults_RemainsClamped_ToRange1To10()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultMemoryMaxResults: 15);  // Higher than max

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: null);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should clamp to 10
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 10)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MemoryMaxResults_ClampedLow_ToMinimum1()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var svc = CreateService(memory, capabilityDefaultMemoryMaxResults: 0);  // Lower than min

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: null);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should clamp to 1
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 1)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TelemetryReflects_ActualEffectiveMemoryDecision_WithCapabilityDefaults()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new[]
            {
                new MemoryRecordResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Tenant",
                    null,
                    "Fact",
                    "k1",
                    "body",
                    new Dictionary<string, string>(),
                    "Low",
                    "UserInput",
                    DateTime.UtcNow,
                    DateTime.UtcNow)
            }));

        var telemetry = new Mock<IExecutionTelemetryService>();
        ExecutionTelemetry? last = null;
        telemetry
            .Setup(t => t.TrackAsync(It.IsAny<ExecutionTelemetry>(), It.IsAny<CancellationToken>()))
            .Callback<ExecutionTelemetry, CancellationToken>((e, _) => last = e)
            .Returns(Task.CompletedTask);

        var svc = CreateService(
            memory, 
            telemetry: telemetry,
            capabilityDefaultIncludeMemory: true);  // Capability enables memory

        var tenant = Guid.NewGuid();
        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify, uses capability default
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Telemetry should reflect actual effective decision (enabled by capability default)
        Assert.NotNull(last);
        Assert.True(last!.MemoryRequested);
        Assert.Equal(1, last.MemoryItemCount);
    }

    [Fact]
    public async Task ExecuteAsync_TelemetryReflects_ActualMemoryItemCount_WithCapabilityDefaults()
    {
        var tenant = Guid.NewGuid();
        var memory = new Mock<IMemoryService>();
        var itemCount = 3;
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Enumerable.Range(0, itemCount)
                .Select(i => new MemoryRecordResponse(
                    Guid.NewGuid(),
                    tenant,
                    "Tenant",
                    null,
                    "Fact",
                    $"k{i}",
                    $"body{i}",
                    new Dictionary<string, string>(),
                    "Low",
                    "UserInput",
                    DateTime.UtcNow,
                    DateTime.UtcNow))
                .ToArray()));

        var telemetry = new Mock<IExecutionTelemetryService>();
        ExecutionTelemetry? last = null;
        telemetry
            .Setup(t => t.TrackAsync(It.IsAny<ExecutionTelemetry>(), It.IsAny<CancellationToken>()))
            .Callback<ExecutionTelemetry, CancellationToken>((e, _) => last = e)
            .Returns(Task.CompletedTask);

        var svc = CreateService(
            memory, 
            telemetry: telemetry,
            capabilityDefaultIncludeMemory: true);

        var request = new ExecutionRequest(
            TenantId: tenant,
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Telemetry should reflect actual item count
        Assert.NotNull(last);
        Assert.True(last!.MemoryRequested);
        Assert.Equal(itemCount, last.MemoryItemCount);
    }

    [Fact]
    public async Task ExecuteAsync_SystemSafeDefault_IncludeMemoryFalse_WhenNothingSpecified()
    {
        var memory = new Mock<IMemoryService>();

        var svc = CreateService(memory);  // No defaults specified

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // System safe default should be false
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithMemoryMaxResultsNull_UsesDefault5()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: null);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 5)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithMemoryMaxResultsOver10_ClampsTo10()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: 15);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 10)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithMemoryMaxResultsUnder1_ClampsTo1()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new List<MemoryRecordResponse>()));

        var svc = CreateService(memory);

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"),
            MemoryMaxResults: 0);

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 1)), Times.Once);
    }

    // ===== PHASE 7: EXECUTION-TIME MEMORY STRATEGY RULES =====

    [Fact]
    public async Task ExecuteAsync_StrategyEnablesMemory_WhenNeitherRequestNorCapabilitySpecifies()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 5, "Strategy enabled for explicit keys"));

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "explicit-key" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Strategy should enable memory
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StrategyDisablesMemory_WhenNeitherRequestNorCapabilitySpecifies()
    {
        var memory = new Mock<IMemoryService>();

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(false, null, "Strategy disabled for broad queries"));

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null));  // Broad list mode

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Strategy should disable memory
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RequestOverrideTakesPrecedenceOverStrategy()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(false, null, "Strategy would disable"));

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: true,  // Request explicitly enables
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Request override should take precedence over strategy
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CapabilityDefaultTakesPrecedenceOverStrategy()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(false, null, "Strategy would disable"));

        var svc = CreateService(
            memory, 
            strategies: new[] { strategy.Object },
            capabilityDefaultIncludeMemory: true);  // Capability enables

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Capability default should take precedence over strategy
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StrategyMaxResultsSuggestion_AppliesWhenNeitherRequestNorCapabilitySpecifies()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 7, "Strategy suggests 7 results"));

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "key" }),
            MemoryMaxResults: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should use strategy's suggested max results
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 7)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RequestMaxResultsTakesPrecedenceOverStrategySuggestion()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 7, "Strategy suggests 7"));

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "key" }),
            MemoryMaxResults: 3);  // Request specifies 3

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Request override should take precedence
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 3)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CapabilityMaxResultsTakesPrecedenceOverStrategySuggestion()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 7, "Strategy suggests 7"));

        var svc = CreateService(
            memory, 
            strategies: new[] { strategy.Object },
            capabilityDefaultMemoryMaxResults: 4);  // Capability specifies 4

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "key" }),
            MemoryMaxResults: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Capability default should take precedence over strategy
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 4)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultStrategy_EnablesMemoryForExplicitKeys()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        // Use the actual DefaultExecutionMemoryStrategy
        var strategy = new DefaultExecutionMemoryStrategy();
        var svc = CreateService(memory, strategies: new[] { strategy });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Request does not specify
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "explicit-key" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Default strategy should enable memory for explicit keys
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultStrategy_EnablesMemoryForSpecificScope()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy = new DefaultExecutionMemoryStrategy();
        var svc = CreateService(memory, strategies: new[] { strategy });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,
            MemoryQuery: new ExecutionMemoryQuery("Session", "session-123", "Fact"));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Default strategy should enable memory for specific scopes
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultStrategy_DisablesMemoryForBroadListMode()
    {
        var memory = new Mock<IMemoryService>();

        var strategy = new DefaultExecutionMemoryStrategy();
        var svc = CreateService(memory, strategies: new[] { strategy });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, null));  // Broad list mode

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Default strategy should disable memory for broad queries
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleStrategies_FirstApplicableWins()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(Array.Empty<MemoryRecordResponse>()));

        var strategy1 = new Mock<IExecutionMemoryStrategy>();
        strategy1.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(false);  // Not applicable

        var strategy2 = new Mock<IExecutionMemoryStrategy>();
        strategy2.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy2.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 8, "Strategy 2 applies"));

        var svc = CreateService(memory, strategies: new[] { strategy1.Object, strategy2.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "key" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should use strategy 2's decision
        memory.Verify(m => m.RetrieveMemoryAsync(It.Is<RetrieveMemoryRequest>(r => r.MaxResults == 8)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoApplicableStrategies_FallsBackToSystemDefault()
    {
        var memory = new Mock<IMemoryService>();

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(false);  // Not applicable

        var svc = CreateService(memory, strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null);  // Request does not specify

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Should fall back to system safe default (false)
        memory.Verify(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StrategyDecision_RecordedInTelemetry()
    {
        var memory = new Mock<IMemoryService>();
        memory.Setup(m => m.RetrieveMemoryAsync(It.IsAny<RetrieveMemoryRequest>()))
            .ReturnsAsync(new RetrieveMemoryResponse(new[]
            {
                new MemoryRecordResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Tenant",
                    null,
                    "Fact",
                    "k1",
                    "body",
                    new Dictionary<string, string>(),
                    "Low",
                    "UserInput",
                    DateTime.UtcNow,
                    DateTime.UtcNow)
            }));

        var telemetry = new Mock<IExecutionTelemetryService>();
        ExecutionTelemetry? last = null;
        telemetry
            .Setup(t => t.TrackAsync(It.IsAny<ExecutionTelemetry>(), It.IsAny<CancellationToken>()))
            .Callback<ExecutionTelemetry, CancellationToken>((e, _) => last = e)
            .Returns(Task.CompletedTask);

        var strategy = new Mock<IExecutionMemoryStrategy>();
        strategy.Setup(s => s.CanHandle(It.IsAny<ExecutionMemoryStrategyContext>()))
            .Returns(true);
        strategy.Setup(s => s.GetDecisionAsync(It.IsAny<ExecutionMemoryStrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionMemoryStrategyDecision(true, 5, "Strategy enabled"));

        var svc = CreateService(
            memory, 
            telemetry: telemetry,
            strategies: new[] { strategy.Object });

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "cap",
            PromptKey: "p",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>(),
            IncludeMemory: null,  // Strategy decides
            MemoryQuery: new ExecutionMemoryQuery("Tenant", null, "Fact", Keys: new[] { "key" }));

        _ = await svc.ExecuteAsync(request, CancellationToken.None);

        // Telemetry should reflect strategy decision
        Assert.NotNull(last);
        Assert.True(last!.MemoryRequested);
        Assert.Equal(1, last.MemoryItemCount);
    }

}
