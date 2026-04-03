using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;

namespace AIL.Modules.PromptRegistry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromptRegistryModule(this IServiceCollection services, bool useInMemory = false, string? persistenceFilePath = null)
    {
        services.AddSingleton<IPromptDefinitionRepository>(sp =>
        {
            var seed = new[]
            {
                new PromptDefinition(
                    PromptKey: "prompt",
                    Version: "v1",
                    Template: "<prompt template>",
                    IsActive: true,
                    Description: "Default prompt for testing",
                    VariableDefinitions: new Dictionary<string, PromptVariableDefinition>
                    {
                        ["k"] = new PromptVariableDefinition("k", true)
                    }),
                new PromptDefinition(
                    PromptKey: "prompt",
                    Version: "v0",
                    Template: "<old template>",
                    IsActive: false,
                    Description: "Inactive old version",
                    VariableDefinitions: new Dictionary<string, PromptVariableDefinition>()),
                new PromptDefinition(
                    PromptKey: "inactive-prompt",
                    Version: "v1",
                    Template: "<inactive template>",
                    IsActive: false,
                    Description: "Example inactive prompt",
                    VariableDefinitions: new Dictionary<string, PromptVariableDefinition>())
            };

            if (useInMemory)
            {
                return new InMemoryPromptDefinitionRepository(seed);
            }

            var path = string.IsNullOrWhiteSpace(persistenceFilePath)
                ? Path.Combine(AppContext.BaseDirectory, "prompt-registry-store.json")
                : persistenceFilePath;

            return new FilePromptDefinitionRepository(path, seed);
        });

        services.AddSingleton<IPromptRegistryService, PromptRegistryService>();
        return services;
    }
}
