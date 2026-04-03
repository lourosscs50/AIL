using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.Security.Infrastructure;
using AIL.Modules.PromptRegistry.Infrastructure;
using AIL.Modules.PolicyRegistry.Infrastructure;
using AIL.Modules.ContextEngine.Infrastructure;
using AIL.Modules.Audit.Infrastructure;
using AIL.Modules.Observability.Infrastructure;
using AIL.Modules.ProviderRegistry.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace AIL.Modules.Execution.Tests;

public sealed class ExecutionServiceTests
{
    private readonly IExecutionService _executionService;

    public ExecutionServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:Mode"] = "Stub"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSecurityModule();
        services.AddPromptRegistryModule(useInMemory: true);
        services.AddPolicyRegistryModule();
        services.AddContextEngineModule();
        services.AddAuditModule();
        services.AddObservabilityModule();
        services.AddProviderRegistryModule();
        services.AddExecutionModule(configuration);

        using var provider = services.BuildServiceProvider();
        _executionService = provider.GetRequiredService<IExecutionService>();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_ForValidTenant()
    {
        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string> { "ref1" });

        var result = await _executionService.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.NotEqual(Guid.Empty, result.AuditRecordId);
        Assert.Equal("v1", result.PromptVersion);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDenied_ForEmptyTenant()
    {
        var request = new ExecutionRequest(
            TenantId: Guid.Empty,
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>());

        var result = await _executionService.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task PromptRegistry_Resolve_ThrowsWhenPromptNotFound()
    {
        using var serviceProvider = new ServiceCollection()
            .AddPromptRegistryModule()
            .BuildServiceProvider();

        var promptRegistry = serviceProvider.GetRequiredService<AIL.Modules.PromptRegistry.Application.IPromptRegistryService>();

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptNotFoundException>(
            () => promptRegistry.ResolvePromptAsync("does-not-exist", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task PromptRegistry_Resolve_ThrowsWhenInactivePromptRequestedVersion()
    {
        using var serviceProvider = new ServiceCollection()
            .AddPromptRegistryModule()
            .BuildServiceProvider();

        var promptRegistry = serviceProvider.GetRequiredService<AIL.Modules.PromptRegistry.Application.IPromptRegistryService>();

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptInactiveException>(
            () => promptRegistry.ResolvePromptAsync("inactive-prompt", "v1", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task PromptRegistry_Resolve_ThrowsWhenMissingRequiredVariable()
    {
        using var serviceProvider = new ServiceCollection()
            .AddPromptRegistryModule()
            .BuildServiceProvider();

        var promptRegistry = serviceProvider.GetRequiredService<AIL.Modules.PromptRegistry.Application.IPromptRegistryService>();

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptValidationException>(
            () => promptRegistry.ResolvePromptAsync("prompt", null, new Dictionary<string, string>(), CancellationToken.None));
    }

    [Fact]
    public async Task PromptRegistry_Resolve_ReturnsHighestActiveVersion_WhenMultipleActiveVersionsExist()
    {
        var customDefinitions = new[]
        {
            new AIL.Modules.PromptRegistry.Domain.PromptDefinition("multi", "v1", "template", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) }),
            new AIL.Modules.PromptRegistry.Domain.PromptDefinition("multi", "v1.10", "template", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) }),
            new AIL.Modules.PromptRegistry.Domain.PromptDefinition("multi", "v2", "template", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) })
        };

        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository(customDefinitions);
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        var resolved = await promptRegistry.ResolvePromptAsync("multi", null, new Dictionary<string, string> { ["k"] = "value" });

        Assert.Equal("v2", resolved.Version);
    }

    [Fact]
    public async Task PromptRegistry_CreatePromptVersion_ThrowsWhenDuplicateVersionExists()
    {
        var existingPrompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition("dup", "v1", "template", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition>());
        var duplicatePrompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition("dup", "v1", "template2", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition>());

        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository(new[] { existingPrompt });
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptAmbiguousException>(
            () => promptRegistry.CreatePromptVersionAsync(duplicatePrompt));
    }

    [Fact]
    public async Task PromptRegistry_Persistence_AddsAndResolvesPromptVersion()
    {
        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository();
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        var newPrompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition(
            PromptKey: "new-prompt",
            Version: "v1",
            Template: "template",
            IsActive: false,
            Description: "New prompt",
            VariableDefinitions: new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition>
            {
                ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true)
            });

        await promptRegistry.CreatePromptVersionAsync(newPrompt);

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptInactiveException>(() => promptRegistry.ResolvePromptAsync("new-prompt"));

        await promptRegistry.ActivatePromptVersionAsync("new-prompt", "v1");

        var resolved = await promptRegistry.ResolvePromptAsync("new-prompt", null, new Dictionary<string, string> { ["k"] = "v" });
        Assert.Equal("v1", resolved.Version);
    }

    [Fact]
    public async Task PromptRegistry_FilePersistence_SurvivesRepositoryRestart()
    {
        var storage = Path.Combine(Path.GetTempPath(), $"prompt-registry-{Guid.NewGuid():N}.json");
        try
        {
            var repository1 = new AIL.Modules.PromptRegistry.Infrastructure.FilePromptDefinitionRepository(storage);
            var promptRegistry1 = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository1);

            var prompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition(
                PromptKey: "persistent-prompt",
                Version: "v1",
                Template: "template",
                IsActive: false,
                Description: "durable prompt",
                VariableDefinitions: new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) });

            await promptRegistry1.CreatePromptVersionAsync(prompt);
            await promptRegistry1.ActivatePromptVersionAsync("persistent-prompt", "v1");

            // re-create repository/service from same storage file path
            var repository2 = new AIL.Modules.PromptRegistry.Infrastructure.FilePromptDefinitionRepository(storage);
            var promptRegistry2 = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository2);

            var resolved = await promptRegistry2.ResolvePromptAsync("persistent-prompt", null, new Dictionary<string, string> { ["k"] = "value" });
            Assert.Equal("v1", resolved.Version);

            // a deactivate should persist too
            await promptRegistry2.DeactivatePromptVersionAsync("persistent-prompt", "v1");

            var repository3 = new AIL.Modules.PromptRegistry.Infrastructure.FilePromptDefinitionRepository(storage);
            var promptRegistry3 = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository3);

            await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptInactiveException>(() => promptRegistry3.ResolvePromptAsync("persistent-prompt", null, new Dictionary<string, string> { ["k"] = "value" }));
        }
        finally
        {
            if (File.Exists(storage))
                File.Delete(storage);
        }
    }

    [Fact]
    public async Task PromptRegistry_Lifecycle_IllegalTransitions_Throw()
    {
        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository();
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptVersionNotFoundException>(
            () => promptRegistry.ActivatePromptVersionAsync("missing", "v1"));

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptVersionNotFoundException>(
            () => promptRegistry.DeactivatePromptVersionAsync("missing", "v1"));
    }

    [Fact]
    public async Task ExecutionService_ResolveUsesUpdatedPromptRegistryState()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Providers:Mode"] = "Stub" })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);

        // override prompt registry repository so we can control lifecycle from test
        var repo = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository();
        services.AddSingleton<AIL.Modules.PromptRegistry.Application.IPromptDefinitionRepository>(repo);
        services.AddSingleton<AIL.Modules.PromptRegistry.Application.IPromptRegistryService, AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService>();

        services.AddSecurityModule();
        services.AddPolicyRegistryModule();
        services.AddContextEngineModule();
        services.AddAuditModule();
        services.AddObservabilityModule();
        services.AddProviderRegistryModule();
        services.AddExecutionModule(config);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<AIL.Modules.PromptRegistry.Application.IPromptRegistryService>();
        var execution = provider.GetRequiredService<IExecutionService>();

        var prompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition(
            PromptKey: "exec-prompt",
            Version: "v1",
            Template: "template",
            IsActive: false,
            Description: "",
            VariableDefinitions: new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) });

        await registry.CreatePromptVersionAsync(prompt);
        await registry.ActivatePromptVersionAsync("exec-prompt", "v1");

        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "exec-prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string>());

        var result = await execution.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal("v1", result.PromptVersion);
    }

    [Fact]
    public async Task ExecutionService_DoesNotIncludeVariableValuesInContextText()
    {
        var request = new ExecutionRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string> { "ref1" });

        var result = await _executionService.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.DoesNotContain("k=v", result.OutputText);
        Assert.Contains("vars=[k]", result.OutputText);
    }

    [Fact]
    public void ExecutionService_DependsOnlyOnPromptRegistryAbstraction()
    {
        var constructor = typeof(AIL.Modules.Execution.Infrastructure.ExecutionService).GetConstructors().Single();

        Assert.Contains(constructor.GetParameters(), p => p.ParameterType == typeof(AIL.Modules.PromptRegistry.Application.IPromptRegistryService));
        Assert.DoesNotContain(constructor.GetParameters(), p => p.ParameterType == typeof(AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService));
    }

    [Fact]
    public async Task PromptRegistry_ThrowsOnInvalidVersionFormat()
    {
        var invalidPrompt = new AIL.Modules.PromptRegistry.Domain.PromptDefinition("invalid", "v1.2b", "template", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition>());
        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository();
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        await Assert.ThrowsAsync<AIL.Modules.PromptRegistry.Domain.PromptValidationException>(
            () => promptRegistry.CreatePromptVersionAsync(invalidPrompt));
    }

    [Fact]
    public async Task PromptRegistry_ResolvesSpecifiedVersionWhenProvidedEvenIfHigherActiveExists()
    {
        var repository = new AIL.Modules.PromptRegistry.Infrastructure.InMemoryPromptDefinitionRepository(new[]
        {
            new AIL.Modules.PromptRegistry.Domain.PromptDefinition("scalar", "v1", "t1", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) }),
            new AIL.Modules.PromptRegistry.Domain.PromptDefinition("scalar", "v2", "t2", true, "", new Dictionary<string, AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition> { ["k"] = new AIL.Modules.PromptRegistry.Domain.PromptVariableDefinition("k", true) })
        });
        var promptRegistry = new AIL.Modules.PromptRegistry.Infrastructure.PromptRegistryService(repository);

        var resolved = await promptRegistry.ResolvePromptAsync("scalar", "v1", new Dictionary<string, string> { ["k"] = "value" });

        Assert.Equal("v1", resolved.Version);
    }
}


