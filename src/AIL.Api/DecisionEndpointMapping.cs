using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Api.Contracts;
using AIL.Modules.Decision.Application;
using AppDecisionRequest = AIL.Modules.Decision.Application.DecisionRequest;

namespace AIL.Api;

/// <summary>
/// Maps the public <see cref="DecideRequest"/> / <see cref="DecideResponse"/> contracts to the Decision application layer and back.
/// Validates bounded, operator-safe payloads at the API boundary (internal callers of <see cref="IDecisionService"/> are unchanged).
/// </summary>
public static class DecisionEndpointMapping
{
    public const int MaxStructuredContextEntries = 64;
    public const int MaxStructuredContextKeyLength = 128;
    public const int MaxStructuredContextValueLength = 4096;
    public const int MaxMetadataEntries = 32;
    public const int MaxMetadataKeyLength = 128;
    public const int MaxMetadataValueLength = 2048;
    public const int MaxContextTextLength = 65536;
    public const int MaxCandidateStrategies = 32;
    public const int MaxCandidateStrategyKeyLength = 256;
    public const int MaxMemoryQueryKeys = 50;
    public const int MaxMemoryQueryKeyLength = 256;
    public const int MaxMemoryTakeRecent = 500;
    public const int MaxMemoryInfluenceSummaryFilterLength = 64;

    /// <summary>Maximum <c>pageSize</c> for <c>GET /decisions/history</c> (matches store defensive cap).</summary>
    public const int MaxDecisionHistoryListPageSize = 100;

    /// <summary>Default <c>pageSize</c> when the query parameter is omitted.</summary>
    public const int DefaultDecisionHistoryListPageSize = 50;

    /// <summary>Maximum <c>page</c> for <c>GET /decisions/history</c> to keep skip offsets bounded.</summary>
    public const int MaxDecisionHistoryListPage = 1_000_000;

    /// <summary>Maximum length of <c>decisionType</c> filter (matches durable column bound).</summary>
    public const int MaxDecisionHistoryDecisionTypeFilterLength = 512;

    /// <summary>Maximum length of <c>selectedStrategyKey</c> filter (matches durable column bound).</summary>
    public const int MaxDecisionHistorySelectedStrategyKeyFilterLength = 512;

    /// <summary>Maximum length of <c>policyKey</c> filter (matches durable column bound).</summary>
    public const int MaxDecisionHistoryPolicyKeyFilterLength = 512;

    /// <summary>API value for <see cref="DecisionHistorySortBy.CreatedAtUtc"/> (bounded list sort).</summary>
    public const string DecisionHistorySortByCreatedAtUtc = "createdAtUtc";

    public const string DecisionHistorySortDirectionAsc = "asc";
    public const string DecisionHistorySortDirectionDesc = "desc";

    public static DecisionHistorySortBy ParseDecisionHistorySortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return DecisionHistorySortBy.CreatedAtUtc;
        if (string.Equals(sortBy.Trim(), DecisionHistorySortByCreatedAtUtc, StringComparison.OrdinalIgnoreCase))
            return DecisionHistorySortBy.CreatedAtUtc;
        throw new ArgumentException(
            $"sortBy must be '{DecisionHistorySortByCreatedAtUtc}' or omitted.",
            nameof(sortBy));
    }

    public static DecisionHistorySortDirection ParseDecisionHistorySortDirection(string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortDirection))
            return DecisionHistorySortDirection.Descending;
        var s = sortDirection.Trim();
        if (string.Equals(s, DecisionHistorySortDirectionAsc, StringComparison.OrdinalIgnoreCase))
            return DecisionHistorySortDirection.Ascending;
        if (string.Equals(s, DecisionHistorySortDirectionDesc, StringComparison.OrdinalIgnoreCase))
            return DecisionHistorySortDirection.Descending;
        throw new ArgumentException("sortDirection must be 'asc' or 'desc'.", nameof(sortDirection));
    }

    public static string ToDecisionHistorySortByApiValue(DecisionHistorySortBy sortBy) =>
        sortBy switch
        {
            DecisionHistorySortBy.CreatedAtUtc => DecisionHistorySortByCreatedAtUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy)),
        };

    public static string ToDecisionHistorySortDirectionApiValue(DecisionHistorySortDirection direction) =>
        direction == DecisionHistorySortDirection.Ascending
            ? DecisionHistorySortDirectionAsc
            : DecisionHistorySortDirectionDesc;

    /// <summary>
    /// Normalizes omitted paging to defaults; rejects explicit out-of-range <paramref name="page"/> or <paramref name="pageSize"/>.
    /// Omitted parameters use page <c>1</c> and pageSize <see cref="DefaultDecisionHistoryListPageSize"/>.
    /// </summary>
    /// <returns><c>true</c> when paging is valid; otherwise <paramref name="error"/> describes the failure.</returns>
    public static bool TryNormalizeDecisionHistoryListPaging(
        int? page,
        int? pageSize,
        out int normalizedPage,
        out int normalizedPageSize,
        out string? error)
    {
        normalizedPage = 1;
        normalizedPageSize = DefaultDecisionHistoryListPageSize;
        error = null;

        if (page is int p)
        {
            if (p < 1)
            {
                error = "page must be at least 1.";
                return false;
            }

            if (p > MaxDecisionHistoryListPage)
            {
                error = $"page must not exceed {MaxDecisionHistoryListPage}.";
                return false;
            }

            normalizedPage = p;
        }

        if (pageSize is int ps)
        {
            if (ps < 1)
            {
                error = "pageSize must be at least 1.";
                return false;
            }

            if (ps > MaxDecisionHistoryListPageSize)
            {
                error = $"pageSize must not exceed {MaxDecisionHistoryListPageSize}.";
                return false;
            }

            normalizedPageSize = ps;
        }

        return true;
    }

    /// <summary>
    /// Builds a tenant-scoped list query with normalized filter strings (caller trims text filters as needed).
    /// </summary>
    public static DecisionHistoryListQuery CreateDecisionHistoryListQuery(
        Guid tenantId,
        int page,
        int pageSize,
        string? decisionType,
        string? selectedStrategyKey,
        string? policyKey,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        Guid? correlationGroupId,
        string? memoryInfluenceSummary,
        Guid? executionInstanceId,
        DecisionHistorySortBy sortBy,
        DecisionHistorySortDirection sortDirection) =>
        new(
            TenantId: tenantId,
            Page: page,
            PageSize: pageSize,
            DecisionType: decisionType,
            SelectedStrategyKey: selectedStrategyKey,
            PolicyKey: policyKey,
            CreatedFromUtc: createdFromUtc,
            CreatedToUtc: createdToUtc,
            CorrelationGroupId: correlationGroupId,
            MemoryInfluenceSummary: memoryInfluenceSummary,
            ExecutionInstanceId: executionInstanceId,
            SortBy: sortBy,
            SortDirection: sortDirection);

    public static AppDecisionRequest MapToDecisionRequest(DecideRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);
        ValidateDecideRequest(req);

        DecisionMemoryQuery? memoryQuery = null;
        if (req.IncludeMemory && req.MemoryQuery is not null)
        {
            var mq = req.MemoryQuery;
            IReadOnlyList<string>? keys = mq.Keys is { Count: > 0 } ? mq.Keys : null;
            memoryQuery = new DecisionMemoryQuery(
                ScopeType: mq.ScopeType,
                ScopeId: mq.ScopeId,
                MemoryKind: mq.MemoryKind,
                Keys: keys,
                TakeRecent: mq.TakeRecent,
                IncludeMetadata: mq.IncludeMetadata);
        }

        return new AppDecisionRequest(
            TenantId: req.TenantId,
            DecisionType: req.DecisionType,
            SubjectType: req.SubjectType,
            SubjectId: req.SubjectId,
            ContextText: req.ContextText,
            StructuredContext: req.StructuredContext,
            IncludeMemory: req.IncludeMemory,
            MemoryQuery: memoryQuery,
            CandidateStrategies: req.CandidateStrategies,
            Metadata: req.Metadata,
            CorrelationGroupId: req.CorrelationGroupId,
            ExecutionInstanceId: req.ExecutionInstanceId);
    }

    /// <summary>
    /// Maps a domain result to the public API contract. <paramref name="correlationGroupId"/> and
    /// <paramref name="executionInstanceId"/> are explicit request pass-through only (never inferred).
    /// </summary>
    public static DecideResponse MapToDecideResponse(
        DecisionResult result,
        Guid? decisionRecordId = null,
        Guid? correlationGroupId = null,
        Guid? executionInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var options = result.Options.Select(o => new DecideOptionResponse(
            OptionId: o.OptionId,
            Confidence: o.Confidence.ToString(),
            Strength: o.Strength,
            RationaleSummary: o.RationaleSummary)).ToList();

        var selectedOptionId = options.Exists(o => o.OptionId == result.SelectedStrategyKey)
            ? result.SelectedStrategyKey
            : null;

        return new DecideResponse(
            DecisionType: result.DecisionType,
            SelectedStrategyKey: result.SelectedStrategyKey,
            Confidence: result.Confidence.ToString(),
            ReasonSummary: result.ReasonSummary,
            Options: options,
            ConsideredStrategies: result.ConsideredStrategies,
            UsedMemory: result.UsedMemory,
            MemoryItemCount: result.MemoryItemCount,
            MemoryInfluenceSummary: result.MemoryInfluenceSummary,
            PolicyKey: result.PolicyKey,
            Metadata: null,
            SelectedOptionId: selectedOptionId,
            DecisionRecordId: decisionRecordId,
            CorrelationGroupId: correlationGroupId,
            ExecutionInstanceId: executionInstanceId);
    }

    /// <summary>
    /// Validates optional history list filters (throws <see cref="ArgumentException"/> for bad combinations).
    /// Empty GUIDs are rejected for correlation/execution filters; string filters are length-bounded to match durable columns and operator-safe retrieval.
    /// Pass non-null strings only for filters the caller will apply (already trimmed; omit or pass null for absent filters).
    /// </summary>
    public static void ValidateDecisionHistoryListFilters(
        Guid? correlationGroupId,
        string? memoryInfluenceSummary,
        Guid? executionInstanceId = null,
        string? decisionType = null,
        string? selectedStrategyKey = null,
        string? policyKey = null)
    {
        if (correlationGroupId is Guid g && g == Guid.Empty)
            throw new ArgumentException("correlationGroupId must not be empty when provided.", nameof(correlationGroupId));

        if (executionInstanceId is Guid ex && ex == Guid.Empty)
            throw new ArgumentException("executionInstanceId must not be empty when provided.", nameof(executionInstanceId));

        if (memoryInfluenceSummary is { Length: > MaxMemoryInfluenceSummaryFilterLength })
            throw new ArgumentException(
                $"memoryInfluenceSummary filter must not exceed {MaxMemoryInfluenceSummaryFilterLength} characters.",
                nameof(memoryInfluenceSummary));

        if (decisionType is { Length: > MaxDecisionHistoryDecisionTypeFilterLength })
            throw new ArgumentException(
                $"decisionType filter must not exceed {MaxDecisionHistoryDecisionTypeFilterLength} characters.",
                nameof(decisionType));

        if (selectedStrategyKey is { Length: > MaxDecisionHistorySelectedStrategyKeyFilterLength })
            throw new ArgumentException(
                $"selectedStrategyKey filter must not exceed {MaxDecisionHistorySelectedStrategyKeyFilterLength} characters.",
                nameof(selectedStrategyKey));

        if (policyKey is { Length: > MaxDecisionHistoryPolicyKeyFilterLength })
            throw new ArgumentException(
                $"policyKey filter must not exceed {MaxDecisionHistoryPolicyKeyFilterLength} characters.",
                nameof(policyKey));
    }

    public static void ValidateDecideRequest(DecideRequest req)
    {
        if (req.ContextText is { Length: > MaxContextTextLength })
            throw new ArgumentException(
                $"ContextText must not exceed {MaxContextTextLength} characters.",
                nameof(req));

        if (req.StructuredContext is not null)
        {
            if (req.StructuredContext.Count > MaxStructuredContextEntries)
                throw new ArgumentException(
                    $"StructuredContext must not contain more than {MaxStructuredContextEntries} entries.",
                    nameof(req));

            foreach (var kv in req.StructuredContext)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    throw new ArgumentException("StructuredContext keys must be non-empty.", nameof(req));

                if (kv.Key.Length > MaxStructuredContextKeyLength)
                    throw new ArgumentException(
                        $"StructuredContext key length must not exceed {MaxStructuredContextKeyLength}.",
                        nameof(req));

                if (kv.Value is { Length: > MaxStructuredContextValueLength })
                    throw new ArgumentException(
                        $"StructuredContext value length must not exceed {MaxStructuredContextValueLength}.",
                        nameof(req));
            }
        }

        if (req.Metadata is not null)
        {
            if (req.Metadata.Count > MaxMetadataEntries)
                throw new ArgumentException(
                    $"Metadata must not contain more than {MaxMetadataEntries} entries.",
                    nameof(req));

            foreach (var kv in req.Metadata)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    throw new ArgumentException("Metadata keys must be non-empty.", nameof(req));

                if (kv.Key.Length > MaxMetadataKeyLength)
                    throw new ArgumentException(
                        $"Metadata key length must not exceed {MaxMetadataKeyLength}.",
                        nameof(req));

                if (kv.Value is { Length: > MaxMetadataValueLength })
                    throw new ArgumentException(
                        $"Metadata value length must not exceed {MaxMetadataValueLength}.",
                        nameof(req));
            }
        }

        if (req.CandidateStrategies is not null)
        {
            if (req.CandidateStrategies.Count > MaxCandidateStrategies)
                throw new ArgumentException(
                    $"CandidateStrategies must not contain more than {MaxCandidateStrategies} entries.",
                    nameof(req));

            foreach (var key in req.CandidateStrategies)
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("CandidateStrategies entries must be non-empty.", nameof(req));

                if (key.Length > MaxCandidateStrategyKeyLength)
                    throw new ArgumentException(
                        $"CandidateStrategies entry length must not exceed {MaxCandidateStrategyKeyLength}.",
                        nameof(req));
            }
        }

        if (req.IncludeMemory && req.MemoryQuery is not null)
            ValidateMemoryQuery(req.MemoryQuery);
    }

    private static void ValidateMemoryQuery(DecideMemoryQueryRequest mq)
    {
        ArgumentNullException.ThrowIfNull(mq);

        if (mq.TakeRecent is int take && (take < 1 || take > MaxMemoryTakeRecent))
            throw new ArgumentException(
                $"TakeRecent must be between 1 and {MaxMemoryTakeRecent}.",
                nameof(mq));

        if (mq.Keys is not { Count: > 0 })
            return;

        if (mq.Keys.Count > MaxMemoryQueryKeys)
            throw new ArgumentException(
                $"Memory query Keys must not contain more than {MaxMemoryQueryKeys} entries.",
                nameof(mq));

        foreach (var k in mq.Keys)
        {
            if (string.IsNullOrWhiteSpace(k))
                throw new ArgumentException("Memory query Keys entries must be non-empty.", nameof(mq));

            if (k.Length > MaxMemoryQueryKeyLength)
                throw new ArgumentException(
                    $"Memory query key length must not exceed {MaxMemoryQueryKeyLength}.",
                    nameof(mq));
        }
    }
}
