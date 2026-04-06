using System;
using System.Collections.Generic;
using System.IO;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryStoreRegistrationTests
{
    [Fact]
    public void AddDecisionHistoryStore_Registers_Singleton_IDecisionHistoryStore()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_reg_{Guid.NewGuid():N}.db");
        try
        {
            var config = new ConfigurationBuilder()
                .Add(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string?>
                    {
                        ["DecisionHistory:SqliteConnectionString"] = $"Data Source={dbPath}",
                    },
                })
                .Build();
            var services = new ServiceCollection();
            services.AddDecisionHistoryStore(config);
            var sp = services.BuildServiceProvider();
            var a = sp.GetRequiredService<IDecisionHistoryStore>();
            var b = sp.GetRequiredService<IDecisionHistoryStore>();
            Assert.Same(a, b);
        }
        finally
        {
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
