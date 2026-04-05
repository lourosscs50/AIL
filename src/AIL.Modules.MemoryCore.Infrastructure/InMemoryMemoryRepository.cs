using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.MemoryCore.Infrastructure;

public sealed class InMemoryMemoryRepository : IMemoryRepository
{
    private readonly object _sync = new();
    private readonly List<MemoryRecord> _store = new();

    public Task<MemoryRecord> AddAsync(MemoryRecord memoryRecord, CancellationToken cancellationToken = default)
    {
        if (memoryRecord is null)
            throw new ArgumentNullException(nameof(memoryRecord));

        lock (_sync)
        {
            _store.Add(memoryRecord);
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
            var item = _store.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id);
            return Task.FromResult(item);
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
            var item = _store.FirstOrDefault(x =>
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
            var query = _store.Where(x => x.TenantId == filter.TenantId);

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
            var index = _store.FindIndex(x => x.TenantId == memoryRecord.TenantId && x.Id == memoryRecord.Id);
            if (index < 0)
                return Task.FromResult<MemoryRecord?>(null);

            _store[index] = memoryRecord;
            return Task.FromResult<MemoryRecord?>(memoryRecord);
        }
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        lock (_sync)
        {
            var items = _store.Where(x => x.TenantId == tenantId).ToList();
            return Task.FromResult<IReadOnlyList<MemoryRecord>>(items);
        }
    }
}
