using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using AIL.Modules.PromptRegistry.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AIL.Modules.PromptRegistry.Tests;

public class UnitTest1
{
    [Fact]
    public async Task FilePersistenceAcrossInstances()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        try
        {
            // First instance
            var services1 = new ServiceCollection();
            services1.AddPromptRegistryModule(useInMemory: false, persistenceFilePath: tempPath);
            var sp1 = services1.BuildServiceProvider();
            var service1 = sp1.GetRequiredService<IPromptRegistryService>();

            var variables = new Dictionary<string, PromptVariableDefinition>
            {
                ["key"] = new PromptVariableDefinition("key", true)
            };

            await service1.CreatePromptVersionAsync("test-key", "v1", "template", true, "desc", variables);

            // Dispose first instance
            await sp1.DisposeAsync();

            // Second instance
            var services2 = new ServiceCollection();
            services2.AddPromptRegistryModule(useInMemory: false, persistenceFilePath: tempPath);
            var sp2 = services2.BuildServiceProvider();
            var service2 = sp2.GetRequiredService<IPromptRegistryService>();

            var prompts = await service2.GetByKeyAsync("test-key");
            var prompt = prompts.Single();

            Assert.Equal("test-key", prompt.PromptKey);
            Assert.Equal("v1", prompt.Version);
            Assert.Equal("template", prompt.Template);
            Assert.True(prompt.IsActive);
            Assert.Equal("desc", prompt.Description);
            Assert.Single(prompt.VariableDefinitions);
            Assert.True(prompt.VariableDefinitions["key"].Required);

            await sp2.DisposeAsync();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}