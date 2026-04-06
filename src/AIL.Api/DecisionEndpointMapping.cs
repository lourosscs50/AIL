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
            CorrelationGroupId: req.CorrelationGroupId);
    }

    /// <summary>
    /// Maps a domain result to the public API contract. <paramref name="correlationGroupId"/> is explicit request pass-through only.
    /// </summary>
    public static DecideResponse MapToDecideResponse(
        DecisionResult result,
        Guid? decisionRecordId = null,
        Guid? correlationGroupId = null)
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
            CorrelationGroupId: correlationGroupId);
    }

    /// <summary>
    /// Validates optional history list filters (throws <see cref="ArgumentException"/> for bad combinations).
    /// </summary>
    public static void ValidateDecisionHistoryListFilters(Guid? correlationGroupId, string? memoryInfluenceSummary)
    {
        if (correlationGroupId is Guid g && g == Guid.Empty)
            throw new ArgumentException("correlationGroupId must not be empty when provided.", nameof(correlationGroupId));

        if (memoryInfluenceSummary is { Length: > MaxMemoryInfluenceSummaryFilterLength })
            throw new ArgumentException(
                $"memoryInfluenceSummary filter must not exceed {MaxMemoryInfluenceSummaryFilterLength} characters.",
                nameof(memoryInfluenceSummary));
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
