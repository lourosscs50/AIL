using AIL.Modules.PromptRegistry.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.PromptRegistry.Application;

public interface IPromptDefinitionRepository
{
    Task<IEnumerable<PromptDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PromptDefinition?> GetByKeyAndVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default);
    Task<IEnumerable<PromptDefinition>> GetByKeyAsync(string promptKey, CancellationToken cancellationToken = default);
    Task AddAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default);
    Task UpdateAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default);
}
