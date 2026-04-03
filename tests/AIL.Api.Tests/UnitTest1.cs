using System.Net;
using System.Net.Http.Json;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

public class ExecuteIntelligenceEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExecuteIntelligenceEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_ExecuteIntelligence_Returns200_OnAllowedTenant()
    {
        var client = _factory.CreateClient();

        var request = new ExecuteIntelligenceRequest(
            TenantId: Guid.NewGuid(),
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string> { ["k"] = "v" },
            ContextReferenceIds: new List<string> { "ref1" });

        var response = await client.PostAsJsonAsync("/execute-intelligence", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ExecuteIntelligenceResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.OutputText);
        Assert.NotEqual(Guid.Empty, body.AuditRecordId);
    }

    [Fact]
    public async Task Post_ExecuteIntelligence_Returns403_OnDeniedTenant()
    {
        var client = _factory.CreateClient();

        var request = new ExecuteIntelligenceRequest(
            TenantId: Guid.Empty,
            CapabilityKey: "capability",
            PromptKey: "prompt",
            Variables: new Dictionary<string, string>(),
            ContextReferenceIds: new List<string>());

        var response = await client.PostAsJsonAsync("/execute-intelligence", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
