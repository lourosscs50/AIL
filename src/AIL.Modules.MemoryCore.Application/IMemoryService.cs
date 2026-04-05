using AIL.Modules.MemoryCore.Contracts;
using System;
using System.Threading.Tasks;

namespace AIL.Modules.MemoryCore.Application;

public interface IMemoryService
{
    /// <summary>Insert-only create (allows duplicate natural keys).</summary>
    Task<MemoryRecordResponse> WriteMemoryAsync(CreateMemoryRequest request);

    /// <summary>Upsert by tenant + scope + memory kind + key when <see cref="CreateMemoryRequest.Key"/> is set; otherwise inserts like <see cref="WriteMemoryAsync"/>.</summary>
    Task<MemoryRecordResponse> StoreMemoryAsync(CreateMemoryRequest request);

    Task<MemoryRecordResponse?> GetMemoryByIdAsync(Guid tenantId, Guid id);
    Task<MemoryRecordResponse?> GetMemoryByKeyAsync(GetMemoryByKeyRequest request);
    Task<MemoryListResult> ListMemoryAsync(ListMemoryRequest request);
    Task<MemoryRecordResponse?> UpdateMemoryAsync(UpdateMemoryRequest request);
    Task<RetrieveMemoryResponse> RetrieveMemoryAsync(RetrieveMemoryRequest request);
}