using System;
using AIL.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AIL.Api.Tests;

/// <summary>
/// Operational hardening: invalid durable-store configuration must fail host startup, not hide behind lazy init.
/// </summary>
public sealed class DecisionHistoryDurableStoreStartupTests
{
    [Fact]
    public void CreateClient_Throws_When_DecisionHistorySqliteConnectionString_HasNoDataSource()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(w =>
        {
            // Host settings apply after default file-based configuration so they win over appsettings.json.
            w.UseSetting("DecisionHistory:SqliteConnectionString", "Data Source=");
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
