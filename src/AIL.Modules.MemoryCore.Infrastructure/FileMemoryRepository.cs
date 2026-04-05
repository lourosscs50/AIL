using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.MemoryCore.Infrastructure;

public sealed class FileMemoryRepository : IMemoryRepository
{
    private readonly string _filePath;
    private readonly object _sync = new object();
    private IReadOnlyDictionary<Guid, MemoryRecord> _store;

    public FileMemoryRepository(string filePath, IEnumerable<MemoryRecord>? seed = null)
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
            if (seed is null) seed = Array.Empty<MemoryRecord>();
            _store = seed.ToDictionary(r => r.Id, r => r);
            Persist();
        }
    }

    public Task<MemoryRecord> AddAsync(MemoryRecord memoryRecord, CancellationToken cancellationToken = default)
    {
        if (memoryRecord is null)
            throw new ArgumentNullException(nameof(memoryRecord));

        lock (_sync)
        {
            if (_store.ContainsKey(memoryRecord.Id))
                throw new InvalidOperationException($"Memory record '{memoryRecord.Id}' already exists.");

            var next = new Dictionary<Guid, MemoryRecord>(_store)
            {
                [memoryRecord.Id] = memoryRecord
            };

            _store = next;
            Persist();
            return Task.FromResult(memoryRecord);
        }
    }

    public Task<MemoryRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (id == Guid.Empty)
            throw new ArgumentException("Id is required.", nameof(id));

        lock (_sync)
        {
            if (!_store.TryGetValue(id, out var record))
                return Task.FromResult<MemoryRecord?>(null);

            return Task.FromResult(record.TenantId == tenantId ? record : null);
        }
    }

    public Task<MemoryRecord?> GetByKeyAsync(MemoryNaturalKey naturalKey, CancellationToken cancellationToken = default)
    {
        if (naturalKey is null)
            throw new ArgumentNullException(nameof(naturalKey));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var scopePersist = naturalKey.ScopeIdForPersistence;
            var item = _store.Values.FirstOrDefault(x =>
                x.TenantId == naturalKey.TenantId &&
                x.ScopeType.Value.Equals(naturalKey.ScopeType.Value, StringComparison.OrdinalIgnoreCase) &&
                ScopeIdEquals(x.ScopeId, scopePersist) &&
                x.MemoryKind.Value.Equals(naturalKey.MemoryKind.Value, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Key, naturalKey.Key, StringComparison.Ordinal));

            return Task.FromResult(item);
        }
    }

    private static bool ScopeIdEquals(string? stored, string persistedForm) =>
        (string.IsNullOrWhiteSpace(stored) ? "" : stored.Trim()).Equals(persistedForm, StringComparison.OrdinalIgnoreCase);

    public Task<MemoryListResult> ListAsync(MemoryListFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
            throw new ArgumentNullException(nameof(filter));

        if (filter.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(filter.TenantId));

        if (filter.PageNumber < 1)
            throw new ArgumentException("PageNumber must be >= 1", nameof(filter.PageNumber));

        if (filter.PageSize < 1 || filter.PageSize > 200)
            throw new ArgumentException("PageSize must be between 1 and 200", nameof(filter.PageSize));

        lock (_sync)
        {
            var query = _store.Values.Where(x => x.TenantId == filter.TenantId);

            if (filter.ScopeType is not null)
                query = query.Where(x => x.ScopeType.Value.Equals(filter.ScopeType.Value, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.ScopeId))
                query = query.Where(x => string.Equals(x.ScopeId, filter.ScopeId, StringComparison.OrdinalIgnoreCase));

            if (filter.MemoryKind is not null)
                query = query.Where(x => x.MemoryKind.Value.Equals(filter.MemoryKind.Value, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.Key))
                query = query.Where(x => string.Equals(x.Key, filter.Key, StringComparison.OrdinalIgnoreCase));

            if (filter.Source is not null)
                query = query.Where(x => x.Source.Value.Equals(filter.Source.Value, StringComparison.OrdinalIgnoreCase));

            if (filter.FromCreatedAtUtc.HasValue)
                query = query.Where(x => x.CreatedAtUtc >= filter.FromCreatedAtUtc.Value);

            if (filter.ToCreatedAtUtc.HasValue)
                query = query.Where(x => x.CreatedAtUtc <= filter.ToCreatedAtUtc.Value);

            var ordered = query
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

            var total = ordered.Count;
            var skip = (filter.PageNumber - 1) * filter.PageSize;
            var pageItems = ordered.Skip(skip).Take(filter.PageSize).ToList();

            return Task.FromResult(new MemoryListResult(pageItems, filter.PageNumber, filter.PageSize, total));
        }
    }

    public Task<MemoryRecord?> UpdateAsync(MemoryRecord memoryRecord, CancellationToken cancellationToken = default)
    {
        if (memoryRecord is null)
            throw new ArgumentNullException(nameof(memoryRecord));

        lock (_sync)
        {
            if (!_store.ContainsKey(memoryRecord.Id))
                return Task.FromResult<MemoryRecord?>(null);

            var next = new Dictionary<Guid, MemoryRecord>(_store)
            {
                [memoryRecord.Id] = memoryRecord
            };

            _store = next;
            Persist();
            return Task.FromResult<MemoryRecord?>(memoryRecord);
        }
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        lock (_sync)
        {
            var items = _store.Values.Where(x => x.TenantId == tenantId).ToList();
            return Task.FromResult<IReadOnlyList<MemoryRecord>>(items);
        }
    }

    private IReadOnlyDictionary<Guid, MemoryRecord> LoadFromFile()
    {
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<Guid, MemoryRecord>();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        var entities = JsonSerializer.Deserialize<List<FileMemoryRecord>>(json, options);
        if (entities is null)
            return new Dictionary<Guid, MemoryRecord>();

        return entities.ToDictionary(e => e.Id, e => e.ToDomain());
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

        var entities = _store.Values.Select(FileMemoryRecord.FromDomain).ToArray();
        var json = JsonSerializer.Serialize(entities, options);
        var tempFile = Path.GetTempFileName();

        File.WriteAllText(tempFile, json);
        File.Copy(tempFile, _filePath, overwrite: true);
        File.Delete(tempFile);
    }

    private sealed record FileMemoryRecord(
        Guid Id,
        Guid TenantId,
        string ScopeTypeValue,
        string? ScopeId,
        string MemoryKindValue,
        string? Key,
        string Content,
        IReadOnlyDictionary<string, string> Metadata,
        string ImportanceValue,
        string SourceValue,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc)
    {
        public static FileMemoryRecord FromDomain(MemoryRecord record)
            => new(
                record.Id,
                record.TenantId,
                record.ScopeType.Value,
                record.ScopeId,
                record.MemoryKind.Value,
                record.Key,
                record.Content,
                new Dictionary<string, string>(record.Metadata, StringComparer.OrdinalIgnoreCase),
                record.Importance.Value,
                record.Source.Value,
                record.CreatedAtUtc,
                record.UpdatedAtUtc);

        public MemoryRecord ToDomain()
            => MemoryRecord.Create(
                Id,
                TenantId,
                MemoryScopeType.Parse(ScopeTypeValue),
                ScopeId,
                MemoryKind.Parse(MemoryKindValue),
                Key,
                Content,
                Metadata,
                MemoryImportance.Parse(ImportanceValue),
                MemorySource.Parse(SourceValue),
                CreatedAtUtc,
                UpdatedAtUtc);
    }
}
