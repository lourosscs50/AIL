using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.PromptRegistry.Infrastructure;

internal sealed class InMemoryPromptDefinitionRepository : IPromptDefinitionRepository
{
    private readonly ConcurrentDictionary<(string Key, string Version), PromptDefinition> _store;

    public InMemoryPromptDefinitionRepository(IEnumerable<PromptDefinition>? seed = null)
    {
        _store = new ConcurrentDictionary<(string Key, string Version), PromptDefinition>(StringTupleComparer.Instance);
        if (seed != null)
        {
            foreach (var prompt in seed)
            {
                _store[(prompt.PromptKey, prompt.Version)] = prompt;
            }
        }
    }

    public Task<IEnumerable<PromptDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.AsEnumerable());

    public Task<PromptDefinition?> GetByKeyAndVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((promptKey, version), out var found);
        return Task.FromResult(found);
    }

    public Task<IEnumerable<PromptDefinition>> GetByKeyAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        var matches = _store.Values.Where(p => string.Equals(p.PromptKey, promptKey, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(matches);
    }

    public Task AddAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default)
    {
        var key = (promptDefinition.PromptKey, promptDefinition.Version);
        if (!_store.TryAdd(key, promptDefinition))
        {
            throw new PromptAmbiguousException(promptDefinition.PromptKey, promptDefinition.Version);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default)
    {
        var key = (promptDefinition.PromptKey, promptDefinition.Version);
        if (!_store.ContainsKey(key))
            throw new PromptNotFoundException(promptDefinition.PromptKey);

        _store[key] = promptDefinition;
        return Task.CompletedTask;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Key, string Version)>
    {
        public static StringTupleComparer Instance { get; } = new StringTupleComparer();

        public bool Equals((string Key, string Version) x, (string Key, string Version) y)
            => string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Key, string Version) obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key)
            ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version);
    }
}
