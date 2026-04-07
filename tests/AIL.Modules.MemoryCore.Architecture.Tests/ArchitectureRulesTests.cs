using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Domain;
using AIL.Modules.MemoryCore.Infrastructure;
using NetArchTest.Rules;
using Xunit;

namespace AIL.Modules.MemoryCore.Architecture.Tests;

public class ArchitectureRulesTests
{
    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(typeof(MemoryScopeType).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.MemoryCore.Domain")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(MemoryScopeType).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.MemoryCore.Domain")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Domain_ShouldNotDependOnContracts()
    {
        var result = Types.InAssembly(typeof(MemoryScopeType).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.MemoryCore.Domain")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Contracts")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(IMemoryService).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.MemoryCore.Application")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Execution_Application_Assembly_ShouldNotDependOn_MemoryCore_Infrastructure()
    {
        var result = Types.InAssembly(typeof(IExecutionService).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Execution_Infrastructure_Assembly_ShouldNotDependOn_MemoryCore_Infrastructure()
    {
        var result = Types.InAssembly(typeof(AIL.Modules.Execution.Infrastructure.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Decision_Application_ShouldNotDependOn_MemoryCore_Infrastructure()
    {
        var result = Types.InAssembly(typeof(IDecisionService).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Decision_Application_ShouldNotDependOn_Decision_Infrastructure()
    {
        var result = Types.InAssembly(typeof(IDecisionService).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.Decision.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Decision_Infrastructure_ShouldNotDependOn_MemoryCore_Infrastructure()
    {
        var result = Types.InAssembly(typeof(AIL.Modules.Decision.Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.MemoryCore.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("\n", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>()));
    }
}
