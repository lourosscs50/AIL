using System;
using System.Collections.Generic;
using System.IO;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryStoreConfigurationHardeningTests
{
    [Fact]
    public void AddDecisionHistoryStore_NullConfiguration_ProductionHost_Throws()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddDecisionHistoryStore(null, env.Object));
        Assert.Contains("requires IConfiguration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDecisionHistoryStore_EmptyConfiguration_ProductionHost_Throws()
    {
        var config = new ConfigurationBuilder()
            .Add(new MemoryConfigurationSource { InitialData = new Dictionary<string, string?>() })
            .Build();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddDecisionHistoryStore(config, env.Object));
        Assert.Contains("explicitly set", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDecisionHistoryStore_Production_WithExplicitSqliteConnectionString_Registers()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_prod_{Guid.NewGuid():N}.db");
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
            var env = new Mock<IHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
            var services = new ServiceCollection();
            services.AddDecisionHistoryStore(config, env.Object);
            var store = services.BuildServiceProvider().GetRequiredService<IDecisionHistoryStore>();
            Assert.NotNull(store);
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    [Fact]
    public void AddDecisionHistoryStore_Development_NoDecisionHistorySection_UsesDevelopmentDefault()
    {
        var config = new ConfigurationBuilder()
            .Add(new MemoryConfigurationSource { InitialData = new Dictionary<string, string?>() })
            .Build();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var services = new ServiceCollection();
        services.AddDecisionHistoryStore(config, env.Object);
        var store = services.BuildServiceProvider().GetRequiredService<IDecisionHistoryStore>();
        Assert.NotNull(store);
    }

    [Fact]
    public void AddDecisionHistoryStore_NullHost_KeepsBackwardCompatibleRegistration()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ail_nullhost_{Guid.NewGuid():N}.db");
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
            services.AddDecisionHistoryStore(config, hostEnvironment: null);
            Assert.NotNull(services.BuildServiceProvider().GetRequiredService<IDecisionHistoryStore>());
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort test cleanup
        }
    }
}
