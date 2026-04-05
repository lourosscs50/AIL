using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Execution.Application;
using AIL.Modules.Security.Domain;
using NetArchTest.Rules;
using Xunit;

namespace AIL.Architecture.Tests;

public class ArchitectureRulesTests
{
    [Fact]
    public void DomainProjects_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(TenantId).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.Security.Domain")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.Security.Infrastructure")
            .GetResult();

        var failures = result.FailingTypes ?? Enumerable.Empty<Type>();
        Assert.True(result.IsSuccessful, string.Join("\n", failures.Select(t => t.FullName)));
    }

    [Fact]
    public void ExecutionApplication_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(IExecutionService).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.Execution.Application")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.Execution.Infrastructure")
            .GetResult();

        var failures = result.FailingTypes ?? Enumerable.Empty<Type>();
        Assert.True(result.IsSuccessful, string.Join("\n", failures.Select(t => t.FullName)));
    }

    [Fact]
    public void ExecutionApplication_Visibility_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(IExecutionService).Assembly)
            .That()
            .ResideInNamespace("AIL.Modules.Execution.Application.Visibility")
            .ShouldNot()
            .HaveDependencyOn("AIL.Modules.Execution.Infrastructure")
            .GetResult();

        var failures = result.FailingTypes ?? Enumerable.Empty<Type>();
        Assert.True(result.IsSuccessful, string.Join("\n", failures.Select(t => t.FullName)));
    }

    [Fact]
    public void ApplicationProjects_ShouldNotDependOnApi()
    {
        var assembly = typeof(IExecutionService).Assembly;
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace("AIL.Modules.Execution.Application")
            .ShouldNot()
            .HaveDependencyOn("AIL.Api")
            .GetResult();

        var failures = result.FailingTypes ?? Enumerable.Empty<Type>();
        Assert.True(result.IsSuccessful, string.Join("\n", failures.Select(t => t.FullName)));
    }

    [Fact]
    public void Decision_Application_ShouldNotDependOn_Api()
    {
        var assembly = typeof(IDecisionService).Assembly;
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace("AIL.Modules.Decision.Application")
            .ShouldNot()
            .HaveDependencyOn("AIL.Api")
            .GetResult();

        var failures = result.FailingTypes ?? Enumerable.Empty<Type>();
        Assert.True(result.IsSuccessful, string.Join("\n", failures.Select(t => t.FullName)));
    }
}
