using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.PromptRegistry.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.PromptRegistry.Infrastructure;

public sealed class FilePromptDefinitionRepository : IPromptDefinitionRepository
{
    private readonly string _filePath;
    private readonly object _sync = new object();
    private IReadOnlyDictionary<(string Key, string Version), PromptDefinition> _store;

    public FilePromptDefinitionRepository(string filePath, IEnumerable<PromptDefinition>? seed = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required", nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(_filePath))
        {
            _store = LoadFromFile();
        }
        else
        {
            if (seed is null) seed = Array.Empty<PromptDefinition>();
            _store = seed
                .Select(p => ((p.PromptKey, p.Version), p))
                .ToDictionary(k => k.Item1, k => k.Item2, StringTupleComparer.Instance);
            Persist();
        }
    }

    public Task<IEnumerable<PromptDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_store.Values.AsEnumerable());
        }
    }

    public Task<PromptDefinition?> GetByKeyAndVersionAsync(string promptKey, string version, CancellationToken cancellationToken = default)
    {
        if (promptKey is null) throw new ArgumentNullException(nameof(promptKey));
        if (version is null) throw new ArgumentNullException(nameof(version));

        lock (_sync)
        {
            _store.TryGetValue((promptKey, version), out var result);
            return Task.FromResult(result);
        }
    }

    public Task<IEnumerable<PromptDefinition>> GetByKeyAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        if (promptKey is null) throw new ArgumentNullException(nameof(promptKey));

        lock (_sync)
        {
            var matches = _store.Values.Where(x => string.Equals(x.PromptKey, promptKey, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(matches);
        }
    }

    public Task AddAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default)
    {
        if (promptDefinition is null)
            throw new ArgumentNullException(nameof(promptDefinition));

        lock (_sync)
        {
            var key = (promptDefinition.PromptKey, promptDefinition.Version);
            if (_store.ContainsKey(key))
                throw new PromptAmbiguousException(promptDefinition.PromptKey, promptDefinition.Version);

            var next = new Dictionary<(string Key, string Version), PromptDefinition>(_store, StringTupleComparer.Instance)
            {
                [key] = promptDefinition
            };

            _store = next;
            Persist();
            return Task.CompletedTask;
        }
    }

    public Task UpdateAsync(PromptDefinition promptDefinition, CancellationToken cancellationToken = default)
    {
        if (promptDefinition is null)
            throw new ArgumentNullException(nameof(promptDefinition));

        lock (_sync)
        {
            var key = (promptDefinition.PromptKey, promptDefinition.Version);
            if (!_store.ContainsKey(key))
                throw new PromptNotFoundException(promptDefinition.PromptKey);

            var next = new Dictionary<(string Key, string Version), PromptDefinition>(_store, StringTupleComparer.Instance)
            {
                [key] = promptDefinition
            };

            _store = next;
            Persist();
            return Task.CompletedTask;
        }
    }

    private IReadOnlyDictionary<(string Key, string Version), PromptDefinition> LoadFromFile()
    {
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<(string, string), PromptDefinition>(StringTupleComparer.Instance);

        var data = JsonSerializer.Deserialize<PromptDefinition[]>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        });

        if (data is null)
            return new Dictionary<(string, string), PromptDefinition>(StringTupleComparer.Instance);

        return data.ToDictionary(p => (p.PromptKey, p.Version), p => p, StringTupleComparer.Instance);
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(_store.Values.ToArray(), options);
        var tempFile = Path.GetTempFileName();

        File.WriteAllText(tempFile, json);
        File.Copy(tempFile, _filePath, overwrite: true);
        File.Delete(tempFile);
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
