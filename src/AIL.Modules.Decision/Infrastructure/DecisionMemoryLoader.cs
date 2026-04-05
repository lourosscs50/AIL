using AIL.Modules.Decision.Application;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Infrastructure;

internal static class DecisionMemoryLoader
{
    internal const int DefaultTakeRecent = 20;

    public static async Task<DecisionMemoryContext> LoadAsync(
        IMemoryService memoryService,
        Guid tenantId,
        DecisionMemoryQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var items = new List<DecisionMemoryContextItem>();

        if (query.Keys is { Count: > 0 })
        {
            foreach (var key in query.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var req = new GetMemoryByKeyRequest(
                    TenantId: tenantId,
                    ScopeType: query.ScopeType,
                    ScopeId: query.ScopeId,
                    MemoryKind: query.MemoryKind!,
                    Key: key);

                var rec = await memoryService.GetMemoryByKeyAsync(req).ConfigureAwait(false);
                if (rec is not null)
                    items.Add(MapResponse(rec, query.IncludeMetadata));
            }
        }
        else
        {
            var take = query.TakeRecent ?? DefaultTakeRecent;

            var listReq = new ListMemoryRequest(
                TenantId: tenantId,
                ScopeType: query.ScopeType,
                ScopeId: query.ScopeId,
                MemoryKind: query.MemoryKind,
                Key: null,
                Source: null,
                FromCreatedAtUtc: null,
                ToCreatedAtUtc: null,
                PageNumber: 1,
                PageSize: take);

            var list = await memoryService.ListMemoryAsync(listReq).ConfigureAwait(false);
            foreach (var r in list.Items)
                items.Add(MapDomain(r, query.IncludeMetadata));
        }

        return new DecisionMemoryContext(items);
    }

    private static void ValidateQuery(DecisionMemoryQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.ScopeType))
            throw new ArgumentException("ScopeType is required for memory retrieval.", nameof(query));

        if (query.Keys is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(query.MemoryKind))
                throw new ArgumentException("MemoryKind is required when Keys are specified.", nameof(query));

            foreach (var k in query.Keys)
            {
                if (string.IsNullOrWhiteSpace(k))
                    throw new ArgumentException("Memory keys cannot be blank.", nameof(query));
            }
        }

        if (query.TakeRecent is int t && (t < 1 || t > 200))
            throw new ArgumentOutOfRangeException("TakeRecent", query.TakeRecent, "TakeRecent must be between 1 and 200.");
    }

    private static DecisionMemoryContextItem MapResponse(MemoryRecordResponse r, bool includeMetadata) =>
        new(
            Key: r.Key,
            Content: r.Content,
            MemoryKind: r.MemoryKind,
            ScopeType: r.ScopeType,
            ScopeId: string.IsNullOrWhiteSpace(r.ScopeId) ? null : r.ScopeId,
            Metadata: includeMetadata ? r.Metadata : null,
            UpdatedAtUtc: r.UpdatedAtUtc);

    private static DecisionMemoryContextItem MapDomain(MemoryRecord r, bool includeMetadata) =>
        new(
            Key: r.Key,
            Content: r.Content,
            MemoryKind: r.MemoryKind.Value,
            ScopeType: r.ScopeType.Value,
            ScopeId: r.ScopeId,
            Metadata: includeMetadata ? r.Metadata : null,
            UpdatedAtUtc: r.UpdatedAtUtc);
}
