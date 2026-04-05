using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIL.Modules.PromptRegistry.Domain;

namespace AIL.Modules.PromptRegistry.Application;

public interface IPromptRegistryService
{
    Task<PromptResolution> ResolvePromptAsync(
        string promptKey,
        string? promptVersion = null,
        IDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default);

    Task<PromptDefinition> CreatePromptVersionAsync(
        PromptDefinition promptDefinition,
        CancellationToken cancellationToken = default);

    Task ActivatePromptVersionAsync(
        string promptKey,
        string version,
        CancellationToken cancellationToken = default);

    Task DeactivatePromptVersionAsync(
        string promptKey,
        string version,
        CancellationToken cancellationToken = default);

    Task<PromptDefinition> PromotePromptVersionAsync(
        string promptKey,
        string version,
        CancellationToken cancellationToken = default);
}
