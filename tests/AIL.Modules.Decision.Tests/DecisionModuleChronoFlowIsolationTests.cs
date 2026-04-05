using AIL.Modules.Decision.Application;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionModuleChronoFlowIsolationTests
{
    [Fact]
    public void Decision_application_assembly_does_not_reference_ChronoFlow()
    {
        var asm = typeof(DecisionRequest).Assembly;
        var bad = asm.GetReferencedAssemblies()
            .Where(a => a.Name?.StartsWith("ChronoFlow", StringComparison.Ordinal) == true)
            .Select(a => a.Name)
            .ToList();

        Assert.Empty(bad);
    }
}
