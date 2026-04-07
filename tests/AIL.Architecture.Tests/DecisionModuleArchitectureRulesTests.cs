using System;
using System.Linq;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;
using NetArchTest.Rules;
using Xunit;

namespace AIL.Architecture.Tests;

/// <summary>
/// Contract tests that lock Decision module dependency directions.
/// These are boundary rules, not behavior tests.
/// </summary>
public sealed class DecisionModuleArchitectureRulesTests
{
    [Fact]
    public void Decision_Domain_ShouldNotDependOn_OuterLayers()
    {
        var result = Types.InAssembly(typeof(DecisionConfidence).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AIL.Api",
                "AIL.Modules.Decision.Application",
                "AIL.Modules.Decision.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Decision_Application_ShouldNotDependOn_Api_Or_DecisionInfrastructure()
    {
        var result = Types.InAssembly(typeof(IDecisionService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AIL.Api",
                "AIL.Modules.Decision.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Decision_Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(typeof(AIL.Modules.Decision.Infrastructure.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AIL.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Decision_Infrastructure_ShouldDependInward_On_DecisionApplication_And_DecisionDomain()
    {
        var dependsOnApplication = Types.InAssembly(typeof(AIL.Modules.Decision.Infrastructure.AssemblyMarker).Assembly)
            .That()
            .HaveName("DecisionService")
            .Should()
            .HaveDependencyOn("AIL.Modules.Decision.Application")
            .GetResult();
        Assert.True(dependsOnApplication.IsSuccessful, FormatFailures(dependsOnApplication));

        var dependsOnDomain = Types.InAssembly(typeof(AIL.Modules.Decision.Infrastructure.AssemblyMarker).Assembly)
            .That()
            .HaveName("DecisionService")
            .Should()
            .HaveDependencyOn("AIL.Modules.Decision.Domain")
            .GetResult();
        Assert.True(dependsOnDomain.IsSuccessful, FormatFailures(dependsOnDomain));
    }

    private static string FormatFailures(TestResult result) =>
        string.Join(Environment.NewLine, (result.FailingTypes ?? Enumerable.Empty<Type>()).Select(t => t.FullName));
}
