using System.Net;
using System.Net.Http.Json;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

public sealed class ExecutionsListEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExecutionsListEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Executions_Returns200_And_Paged_Shape()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/executions?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedDecisionVisibilityResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Page);
        Assert.Equal(10, body.PageSize);
        Assert.NotNull(body.Items);
        Assert.True(body.TotalCount >= 0);
    }
}
