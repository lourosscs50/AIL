using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.MemoryCore.Domain;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AIL.Modules.MemoryCore.Application;

public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MemoryService(IMemoryRepository repository, IDateTimeProvider dateTimeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    public Task<MemoryRecordResponse> StoreMemoryAsync(CreateMemoryRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Key))
            return WriteMemoryAsync(request);

        return StoreWithKeyAsync(request);
    }

    private async Task<MemoryRecordResponse> StoreWithKeyAsync(CreateMemoryRequest request)
    {
        var scopeType = MemoryScopeType.Parse(request.ScopeType);
        var memoryKind = MemoryKind.Parse(request.MemoryKind);
        var naturalKey = new MemoryNaturalKey(request.TenantId, scopeType, request.ScopeId, memoryKind, request.Key!);

        var existing = await _repository.GetByKeyAsync(naturalKey);
        if (existing is null)
            return await WriteMemoryAsync(request);

        var importance = MemoryImportance.Parse(request.Importance);
        var now = _dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("IDateTimeProvider.UtcNow must be UTC.");

        if (now <= existing.UpdatedAtUtc)
            now = existing.UpdatedAtUtc.AddTicks(1);

        var updated = existing.WithUpdatedState(
            updatedContent: request.Content,
            metadata: request.Metadata,
            importance: importance,
            updatedAtUtc: now);

        var persisted = await _repository.UpdateAsync(updated);
        if (persisted is null)
            return await WriteMemoryAsync(request);

        return MapToResponse(persisted);
    }

    public async Task<MemoryRecordResponse> WriteMemoryAsync(CreateMemoryRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var scopeType = MemoryScopeType.Parse(request.ScopeType);
        var memoryKind = MemoryKind.Parse(request.MemoryKind);
        var importance = MemoryImportance.Parse(request.Importance);
        var source = MemorySource.Parse(request.Source);

        var record = MemoryRecord.Create(
            id: Guid.NewGuid(),
            tenantId: request.TenantId,
            scopeType: scopeType,
            scopeId: request.ScopeId,
            memoryKind: memoryKind,
            key: request.Key,
            content: request.Content,
            metadata: request.Metadata,
            importance: importance,
            source: source,
            createdAtUtc: _dateTimeProvider.UtcNow,
            updatedAtUtc: _dateTimeProvider.UtcNow);

        var saved = await _repository.AddAsync(record);
        return MapToResponse(saved);
    }

    public async Task<MemoryRecordResponse?> GetMemoryByKeyAsync(GetMemoryByKeyRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        var scopeType = MemoryScopeType.Parse(request.ScopeType);
        var memoryKind = MemoryKind.Parse(request.MemoryKind);
        var naturalKey = new MemoryNaturalKey(request.TenantId, scopeType, request.ScopeId, memoryKind, request.Key);

        var record = await _repository.GetByKeyAsync(naturalKey);
        return record is null ? null : MapToResponse(record);
    }

    public async Task<MemoryRecordResponse?> GetMemoryByIdAsync(Guid tenantId, Guid id)
    {
        var record = await _repository.GetByIdAsync(tenantId, id);
        return record is null ? null : MapToResponse(record);
    }

    public async Task<MemoryListResult> ListMemoryAsync(ListMemoryRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var scopeType = request.ScopeType is null ? null : MemoryScopeType.Parse(request.ScopeType);
        var memoryKind = request.MemoryKind is null ? null : MemoryKind.Parse(request.MemoryKind);
        var source = request.Source is null ? null : MemorySource.Parse(request.Source);

        var filter = new MemoryListFilter(
            TenantId: request.TenantId,
            ScopeType: scopeType,
            ScopeId: request.ScopeId,
            MemoryKind: memoryKind,
            Key: request.Key,
            Source: source,
            FromCreatedAtUtc: request.FromCreatedAtUtc,
            ToCreatedAtUtc: request.ToCreatedAtUtc,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize);

        return await _repository.ListAsync(filter);
    }

    public async Task<MemoryRecordResponse?> UpdateMemoryAsync(UpdateMemoryRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var existing = await _repository.GetByIdAsync(request.TenantId, request.MemoryId);
        if (existing is null)
            return null;

        var importance = MemoryImportance.Parse(request.Importance);

        var updated = existing.WithUpdatedState(
            updatedContent: request.Content,
            metadata: request.Metadata,
            importance: importance,
            updatedAtUtc: _dateTimeProvider.UtcNow);

        var persisted = await _repository.UpdateAsync(updated);
        if (persisted is null)
            return null;

        return MapToResponse(persisted);
    }

    public async Task<RetrieveMemoryResponse> RetrieveMemoryAsync(RetrieveMemoryRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request.TenantId));

        if (request.MaxResults < 1 || request.MaxResults > 100)
            throw new ArgumentException("MaxResults must be between 1 and 100.", nameof(request.MaxResults));

        var candidates = await _repository.ListAllAsync(request.TenantId);

        // Phase 7 Alternative: Memory Retrieval Optimization Layer
        // 1. Candidate Pruning: Apply importance filter early to reduce ranking workload
        var minImportance = request.MinimumImportance is null ? null : MemoryImportance.Parse(request.MinimumImportance);
        var prunedCandidates = candidates.Where(r =>
            (request.ScopeType == null || r.ScopeType.Value.Equals(request.ScopeType, StringComparison.OrdinalIgnoreCase)) &&
            (request.ScopeId == null || string.Equals(r.ScopeId, request.ScopeId, StringComparison.OrdinalIgnoreCase)) &&
            (request.MemoryKind == null || r.MemoryKind.Value.Equals(request.MemoryKind, StringComparison.OrdinalIgnoreCase)) &&
            (request.Key == null || string.Equals(r.Key, request.Key, StringComparison.Ordinal)) &&
            (request.Source == null || r.Source.Value.Equals(request.Source, StringComparison.OrdinalIgnoreCase)) &&
            (!request.CreatedAfterUtc.HasValue || r.CreatedAtUtc >= request.CreatedAfterUtc.Value) &&
            (!request.CreatedBeforeUtc.HasValue || r.CreatedAtUtc <= request.CreatedBeforeUtc.Value) &&
            (minImportance is null || GetImportanceScore(r.Importance) >= GetImportanceScore(minImportance)));

        // 2. Ranking Refinement: Preserve deterministic ranking order with optimized implementation
        var ranked = prunedCandidates
            .OrderByDescending(r => ExactScopeMatch(r, request))
            .ThenByDescending(r => ExactKeyMatch(r, request))
            .ThenByDescending(r => GetImportanceScore(r.Importance))
            .ThenByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Take(request.MaxResults);

        // 3. Deduplication: Safety check for exact duplicates (by Id) - preserves deterministic ordering
        var deduplicated = ranked
            .GroupBy(r => r.Id)
            .Select(g => g.First()) // Stable deduplication - first occurrence wins
            .OrderByDescending(r => ExactScopeMatch(r, request)) // Re-apply ranking after dedupe
            .ThenByDescending(r => ExactKeyMatch(r, request))
            .ThenByDescending(r => GetImportanceScore(r.Importance))
            .ThenByDescending(r => r.UpdatedAtUtc)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Take(request.MaxResults)
            .Select(MapToResponse)
            .ToList();

        return new RetrieveMemoryResponse(deduplicated);
    }

    private static int ExactScopeMatch(MemoryRecord r, RetrieveMemoryRequest request)
    {
        if (request.ScopeType != null && request.ScopeId != null &&
            r.ScopeType.Value.Equals(request.ScopeType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.ScopeId, request.ScopeId, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 0;
    }

    private static int ExactKeyMatch(MemoryRecord r, RetrieveMemoryRequest request)
    {
        if (request.Key != null && string.Equals(r.Key, request.Key, StringComparison.Ordinal))
            return 1;
        return 0;
    }

    private static int GetImportanceScore(MemoryImportance imp) => imp.Value switch
    {
        "Low" => 1,
        "Medium" => 2,
        "High" => 3,
        "Critical" => 4,
        _ => 0
    };

    private static bool ImportanceGreaterOrEqual(MemoryImportance recordImp, string min)
    {
        var minImp = MemoryImportance.Parse(min);
        return GetImportanceScore(recordImp) >= GetImportanceScore(minImp);
    }

    private static MemoryRecordResponse MapToResponse(MemoryRecord record) =>
        new(
            Id: record.Id,
            TenantId: record.TenantId,
            ScopeType: record.ScopeType.Value,
            ScopeId: record.ScopeId,
            MemoryKind: record.MemoryKind.Value,
            Key: record.Key,
            Content: record.Content,
            Metadata: record.Metadata,
            Importance: record.Importance.Value,
            Source: record.Source.Value,
            CreatedAtUtc: record.CreatedAtUtc,
            UpdatedAtUtc: record.UpdatedAtUtc);
    }