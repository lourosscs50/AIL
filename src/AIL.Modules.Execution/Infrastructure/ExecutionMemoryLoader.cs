using AIL.Modules.Execution.Application;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.MemoryCore.Domain;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal static class ExecutionMemoryLoader
{
    internal const int DefaultTakeRecent = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string FormatContextSuffix(MemoryExecutionContext context) =>
        $" memory_context={JsonSerializer.Serialize(context, JsonOptions)}";

    public static async Task<MemoryExecutionContext> LoadAsync(
        IMemoryService memoryService,
        Guid tenantId,
        ExecutionMemoryQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var items = new List<AIL.Modules.Execution.Application.MemoryContextItem>();

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

        return new MemoryExecutionContext(items);
    }

    private static void ValidateQuery(ExecutionMemoryQuery query)
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

    private static AIL.Modules.Execution.Application.MemoryContextItem MapResponse(MemoryRecordResponse r, bool includeMetadata) =>
        new(
            Key: r.Key,
            Content: r.Content,
            MemoryKind: r.MemoryKind,
            ScopeType: r.ScopeType,
            ScopeId: string.IsNullOrWhiteSpace(r.ScopeId) ? null : r.ScopeId,
            Metadata: includeMetadata ? r.Metadata : null,
            UpdatedAtUtc: r.UpdatedAtUtc);

    private static AIL.Modules.Execution.Application.MemoryContextItem MapDomain(MemoryRecord r, bool includeMetadata) =>
        new(
            Key: r.Key,
            Content: r.Content,
            MemoryKind: r.MemoryKind.Value,
            ScopeType: r.ScopeType.Value,
            ScopeId: r.ScopeId,
            Metadata: includeMetadata ? r.Metadata : null,
            UpdatedAtUtc: r.UpdatedAtUtc);
}
