using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.MemoryCore.Application;

public interface IMemoryRepository
{
    Task<MemoryRecord> AddAsync(MemoryRecord memoryRecord, CancellationToken cancellationToken = default);
    Task<MemoryRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<MemoryRecord?> GetByKeyAsync(MemoryNaturalKey naturalKey, CancellationToken cancellationToken = default);
    Task<MemoryListResult> ListAsync(MemoryListFilter filter, CancellationToken cancellationToken = default);
    Task<MemoryRecord?> UpdateAsync(MemoryRecord memoryRecord, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryRecord>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
