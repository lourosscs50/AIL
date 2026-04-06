using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryStoreRegistrationTests
{
    [Fact]
    public void AddDecisionHistoryStore_Registers_Singleton_IDecisionHistoryStore()
    {
        var services = new ServiceCollection();
        services.AddDecisionHistoryStore();
        var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<IDecisionHistoryStore>();
        var b = sp.GetRequiredService<IDecisionHistoryStore>();
        Assert.Same(a, b);
    }
}
