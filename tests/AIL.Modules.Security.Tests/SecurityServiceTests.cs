using AIL.Modules.Security.Application;
using AIL.Modules.Security.Domain;
using AIL.Modules.Security.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIL.Modules.Security.Tests;

public class SecurityServiceTests
{
    private readonly ISecurityService _securityService;

    public SecurityServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSecurityModule();
        var provider = services.BuildServiceProvider();
        _securityService = provider.GetRequiredService<ISecurityService>();
    }

    [Fact]
    public async Task EvaluateAccessAsync_Denies_WhenTenantIdIsEmpty()
    {
        var decision = await _securityService.EvaluateAccessAsync(new TenantId(string.Empty));

        Assert.False(decision.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public async Task EvaluateAccessAsync_Allows_WhenTenantIdIsValid()
    {
        var decision = await _securityService.EvaluateAccessAsync(new TenantId(Guid.NewGuid().ToString()));

        Assert.True(decision.IsAllowed);
    }
}
