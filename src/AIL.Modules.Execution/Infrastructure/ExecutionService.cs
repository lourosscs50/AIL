using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Application.Visibility;
using AIL.Modules.Security.Application;
using AIL.Modules.ContextEngine.Application;
using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.PolicyRegistry.Domain;
using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using AIL.Modules.Security.Domain;
using AIL.Modules.Observability.Application;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.MemoryCore.Contracts;
using AIL.Modules.PromptRegistry.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class ExecutionService : IExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISecurityService _security;
    private readonly IPromptRegistryService _promptRegistry;
    private readonly IPolicyRegistryService _policyRegistry;
    private readonly IProviderSelectionService _selection;
    private readonly IContextEngineService _contextEngine;
    private readonly IMemoryService _memory;
    private readonly IMemoryContextAssembler _memoryContextAssembler;
    private readonly IExecutionReliabilityService _reliability;
    private readonly IAuditService _audit;
    private readonly IExecutionTelemetryService _telemetry;
    private readonly IExecutionVisibilityReadStore _visibilityStore;
    private readonly IEnumerable<IExecutionMemoryStrategy> _memoryStrategies;

    public ExecutionService(
        ISecurityService security,
        IPromptRegistryService promptRegistry,
        IPolicyRegistryService policyRegistry,
        IProviderSelectionService selection,
        IContextEngineService contextEngineService,
        IMemoryService memoryService,
        IMemoryContextAssembler memoryContextAssembler,
        IExecutionReliabilityService reliability,
        IAuditService audit,
        IExecutionTelemetryService telemetry,
        IExecutionVisibilityReadStore visibilityStore,
        IEnumerable<IExecutionMemoryStrategy> memoryStrategies)
    {
        _security = security;
        _promptRegistry = promptRegistry;
        _policyRegistry = policyRegistry;
        _selection = selection;
        _contextEngine = contextEngineService;
        _memory = memoryService;
        _memoryContextAssembler = memoryContextAssembler;
        _reliability = reliability;
        _audit = audit;
        _telemetry = telemetry;
        _visibilityStore = visibilityStore;
        _memoryStrategies = memoryStrategies;
    }

    private async Task<bool> GetStrategyMemoryDecisionAsync(ExecutionRequest request, ExecutionPolicy policy, CancellationToken cancellationToken)
    {
        var context = new ExecutionMemoryStrategyContext(
            CapabilityKey: request.CapabilityKey,
            MemoryQuery: request.MemoryQuery,
            HasExplicitRequestOverride: request.IncludeMemory.HasValue,
            HasCapabilityDefault: policy.DefaultIncludeMemory.HasValue);

        foreach (var strategy in _memoryStrategies)
        {
            if (strategy.CanHandle(context))
            {
                var decision = await strategy.GetDecisionAsync(context, cancellationToken).ConfigureAwait(false);
                return decision.ShouldUseMemory;
            }
        }

        // No strategy handled the context, use safe default
        return false;
    }

    private async Task<int?> GetStrategyMaxResultsSuggestionAsync(ExecutionRequest request, ExecutionPolicy policy, CancellationToken cancellationToken)
    {
        var context = new ExecutionMemoryStrategyContext(
            CapabilityKey: request.CapabilityKey,
            MemoryQuery: request.MemoryQuery,
            HasExplicitRequestOverride: request.IncludeMemory.HasValue,
            HasCapabilityDefault: policy.DefaultIncludeMemory.HasValue);

        foreach (var strategy in _memoryStrategies)
        {
            if (strategy.CanHandle(context))
            {
                var decision = await strategy.GetDecisionAsync(context, cancellationToken).ConfigureAwait(false);
                return decision.SuggestedMaxResults;
            }
        }

        // No strategy handled the context
        return null;
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var executionInstanceId = request.ExecutionInstanceId ?? Guid.NewGuid();
        var startedUtc = DateTime.UtcNow;
        bool? memoryResolvedForObservability = null;

        try
        {
            var tenantId = request.TenantId == Guid.Empty
                ? null
                : new TenantId(request.TenantId.ToString());

            var accessDecision = await _security.EvaluateAccessAsync(tenantId, cancellationToken);
            if (!accessDecision.IsAllowed)
            {
                stopwatch.Stop();
                var completedUtc = DateTime.UtcNow;
                var deniedVisibility = ExecutionVisibilityReadModelBuilder.BuildDenied(
                    executionInstanceId,
                    request,
                    startedUtc,
                    completedUtc,
                    accessDecision.Reason,
                    request.IncludeMemory.GetValueOrDefault(false));
                _visibilityStore.Put(deniedVisibility);

                var denialAuditId = await _audit.RecordAsync(new AuditRecord(
                    TenantId: request.TenantId,
                    CapabilityKey: request.CapabilityKey,
                    PromptKey: request.PromptKey,
                    PromptVersion: null,
                    PolicyKey: null,
                    ProviderKey: null,
                    ModelKey: null,
                    UsedFallback: false,
                    InputTokenCount: null,
                    OutputTokenCount: null,
                    ContextReferenceIds: request.ContextReferenceIds,
                    ExecutedAtUtc: DateTime.UtcNow,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Outcome: "Denied",
                    Notes: accessDecision.Reason ?? string.Empty),
                    cancellationToken);

                await _telemetry.TrackAsync(new ExecutionTelemetry(
                    TenantId: request.TenantId,
                    CapabilityKey: request.CapabilityKey,
                    PromptKey: request.PromptKey,
                    PromptVersion: null,
                    PolicyKey: null,
                    ProviderKey: "n/a",
                    ModelKey: "n/a",
                    UsedFallback: false,
                    InputTokenCount: null,
                    OutputTokenCount: null,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Succeeded: false,
                    ErrorMessage: "Access denied",
                    MemoryRequested: request.IncludeMemory.GetValueOrDefault(false),
                    MemoryItemCount: null,
                    TraceThreadId: request.TraceThreadId,
                    CorrelationGroupId: request.CorrelationGroupId,
                    ExecutionInstanceId: executionInstanceId),
                    cancellationToken);

                return new ExecutionResult(
                    IsAllowed: false,
                    DenyReason: accessDecision.Reason,
                    OutputText: string.Empty,
                    ProviderKey: string.Empty,
                    ModelKey: string.Empty,
                    PromptVersion: string.Empty,
                    AuditRecordId: denialAuditId,
                    ExecutionInstanceId: executionInstanceId,
                    Visibility: deniedVisibility);
            }

            // Resolve policy early to access capability-level memory defaults
            var policy = await _policyRegistry.ResolvePolicyAsync(request.CapabilityKey, cancellationToken).ConfigureAwait(false);

            // Implement precedence for IncludeMemory:
            // 1. Request-level IncludeMemory explicit override
            // 2. Capability-level DefaultIncludeMemory
            // 3. Execution-time strategy rule
            // 4. System safe default = false
            var memoryRequested = request.IncludeMemory.HasValue
                ? request.IncludeMemory.Value
                : policy.DefaultIncludeMemory.HasValue
                    ? policy.DefaultIncludeMemory.Value
                    : await GetStrategyMemoryDecisionAsync(request, policy, cancellationToken);

            memoryResolvedForObservability = memoryRequested;

            if (memoryRequested && request.MemoryQuery is null)
                throw new ArgumentException("MemoryQuery is required when IncludeMemory is true.", nameof(request));

            MemoryContext? memoryContext = null;
            int? memoryItemCount = null;

            if (memoryRequested)
            {
                // Compute effective max results using precedence:
                // Priority 1: Explicit request.MemoryMaxResults (from ExecutionRequest) - clamped to 1..10
                // Priority 2: Memory query TakeRecent (from ExecutionMemoryQuery) - no clamping (preserves Phase 1-5 behavior)
                // Priority 3: Default for true list mode (no Keys AND no MemoryKind) - no clamping (preserves Phase 1-5 behavior)
                // Priority 4: Capability default MemoryMaxResults - clamped to 1..10
                // Priority 5: Strategy-suggested max results - clamped to 1..10
                // Priority 6: System default (5) - clamped to 1..10
                int maxResults;
                var isListMode = (request.MemoryQuery?.Keys == null || request.MemoryQuery.Keys.Count == 0) 
                                && string.IsNullOrWhiteSpace(request.MemoryQuery?.MemoryKind);

                if (request.MemoryMaxResults.HasValue)
                {
                    // Explicit request override: clamp to 1..10
                    maxResults = Math.Clamp(request.MemoryMaxResults.Value, 1, 10);
                }
                else if (request.MemoryQuery?.TakeRecent.HasValue == true)
                {
                    // TakeRecent from query: use as-is (no clamping to preserve Phase 1-5 behavior)
                    maxResults = request.MemoryQuery.TakeRecent.Value;
                }
                else if (isListMode)
                {
                    // True list mode: use DefaultTakeRecent unless overridden by capability default
                    var effectiveListDefault = policy.DefaultMemoryMaxResults ?? ExecutionMemoryLoader.DefaultTakeRecent;
                    maxResults = Math.Clamp(effectiveListDefault, 1, int.MaxValue); // Clamp min to 1, but not max
                }
                else
                {
                    // Kind-based or key-based mode: use capability default, strategy suggestion, or system default (clamped)
                    var strategySuggestion = await GetStrategyMaxResultsSuggestionAsync(request, policy, cancellationToken);
                    var effectiveDefault = policy.DefaultMemoryMaxResults ?? strategySuggestion ?? 5;
                    maxResults = Math.Clamp(effectiveDefault, 1, 10);
                }

                var retrieveRequest = new RetrieveMemoryRequest(
                    TenantId: request.TenantId,
                    ScopeType: request.MemoryQuery!.ScopeType,
                    ScopeId: request.MemoryQuery.ScopeId,
                    MemoryKind: request.MemoryQuery.MemoryKind,
                    Key: request.MemoryQuery.Keys?.FirstOrDefault(),
                    Source: null,
                    MinimumImportance: null,
                    MaxResults: maxResults,
                    CreatedAfterUtc: null,
                    CreatedBeforeUtc: null);

                var retrieveResponse = await _memory.RetrieveMemoryAsync(retrieveRequest).ConfigureAwait(false);
                memoryContext = await _memoryContextAssembler.AssembleAsync(retrieveResponse).ConfigureAwait(false);
                memoryItemCount = memoryContext.Items.Count;
            }

            var variables = new Dictionary<string, string>(request.Variables);
            if (memoryContext != null)
            {
                variables[PromptReservedVariableNames.MemoryContext] = JsonSerializer.Serialize(memoryContext, JsonOptions);
            }

            var memorySuffix = memoryRequested && memoryContext != null
                ? $" memory_context={JsonSerializer.Serialize(memoryContext, JsonOptions)}"
                : string.Empty;

            var prompt = await _promptRegistry.ResolvePromptAsync(request.PromptKey, null, variables, cancellationToken).ConfigureAwait(false);
            var selection = await _selection.SelectAsync(request.CapabilityKey, cancellationToken).ConfigureAwait(false);
            var context = await _contextEngine.BuildContextAsync(request.ContextReferenceIds, request.Variables, cancellationToken).ConfigureAwait(false);

            var contextText =
                $"refs=[{string.Join(",", context.ReferenceIds)}] vars=[{string.Join(",", context.Variables.Keys.OrderBy(key => key))}]{memorySuffix}";

            var providerStopwatch = Stopwatch.StartNew();

            var metadata = new Dictionary<string, string>
            {
                ["CapabilityKey"] = request.CapabilityKey,
                ["PromptKey"] = request.PromptKey,
                ["PolicyKey"] = policy.PolicyKey,
                ["PrimaryProviderKey"] = selection.PrimaryProviderKey,
                ["PrimaryModelKey"] = selection.PrimaryModelKey,
                ["FallbackProviderKey"] = selection.FallbackProviderKey ?? string.Empty,
                ["FallbackModelKey"] = selection.FallbackModelKey ?? string.Empty,
                ["FallbackAllowed"] = selection.FallbackAllowed.ToString()
            };

            metadata["MemoryRequested"] = memoryRequested.ToString();
            if (memoryRequested)
                metadata["MemoryItemCount"] = (memoryItemCount ?? 0).ToString();

            var providerResult = await _reliability.ExecuteWithReliabilityAsync(
                new ProviderExecutionRequest(
                    TenantId: request.TenantId,
                    CapabilityKey: request.CapabilityKey,
                    PromptKey: request.PromptKey,
                    PromptVersion: prompt.Version,
                    PromptText: prompt.Template,
                    ContextText: contextText,
                    MaxTokens: selection.MaxTokens,
                    FallbackAllowed: selection.FallbackAllowed,
                    PrimaryProviderKey: selection.PrimaryProviderKey,
                    PrimaryModelKey: selection.PrimaryModelKey,
                    FallbackProviderKey: selection.FallbackProviderKey,
                    FallbackModelKey: selection.FallbackModelKey,
                    Metadata: metadata),
                cancellationToken).ConfigureAwait(false);

            providerStopwatch.Stop();

            var auditNotes = memoryRequested
                ? $"Provider call took {providerStopwatch.ElapsedMilliseconds}ms; memory_item_count={memoryItemCount ?? 0}"
                : $"Provider call took {providerStopwatch.ElapsedMilliseconds}ms";

            var auditId = await _audit.RecordAsync(new AuditRecord(
                TenantId: request.TenantId,
                CapabilityKey: request.CapabilityKey,
                PromptKey: request.PromptKey,
                PromptVersion: prompt.Version,
                PolicyKey: policy.PolicyKey,
                ProviderKey: providerResult.ProviderKey,
                ModelKey: providerResult.ModelKey,
                UsedFallback: providerResult.UsedFallback,
                InputTokenCount: providerResult.InputTokenCount,
                OutputTokenCount: providerResult.OutputTokenCount,
                ContextReferenceIds: request.ContextReferenceIds,
                ExecutedAtUtc: DateTime.UtcNow,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Outcome: "Allowed",
                Notes: auditNotes),
                cancellationToken).ConfigureAwait(false);

            await _telemetry.TrackAsync(new ExecutionTelemetry(
                TenantId: request.TenantId,
                CapabilityKey: request.CapabilityKey,
                PromptKey: request.PromptKey,
                PromptVersion: prompt.Version,
                PolicyKey: policy.PolicyKey,
                ProviderKey: providerResult.ProviderKey,
                ModelKey: providerResult.ModelKey,
                UsedFallback: providerResult.UsedFallback,
                InputTokenCount: providerResult.InputTokenCount,
                OutputTokenCount: providerResult.OutputTokenCount,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Succeeded: true,
                ErrorMessage: null,
                MemoryRequested: memoryRequested,
                MemoryItemCount: memoryItemCount,
                TraceThreadId: request.TraceThreadId,
                CorrelationGroupId: request.CorrelationGroupId,
                ExecutionInstanceId: executionInstanceId),
                cancellationToken).ConfigureAwait(false);

            var successCompletedUtc = DateTime.UtcNow;
            var successVisibility = ExecutionVisibilityReadModelBuilder.BuildSucceeded(
                executionInstanceId,
                request,
                startedUtc,
                successCompletedUtc,
                policy.PolicyKey,
                selection,
                providerResult,
                prompt.Version,
                memoryRequested,
                memoryItemCount,
                providerResult.OutputText);
            _visibilityStore.Put(successVisibility);

            return new ExecutionResult(
                IsAllowed: true,
                DenyReason: null,
                OutputText: providerResult.OutputText,
                ProviderKey: providerResult.ProviderKey,
                ModelKey: providerResult.ModelKey,
                PromptVersion: prompt.Version,
                AuditRecordId: auditId,
                ExecutionInstanceId: executionInstanceId,
                Visibility: successVisibility);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var faultCompletedUtc = DateTime.UtcNow;
            var memoryForFault = memoryResolvedForObservability ?? request.IncludeMemory.GetValueOrDefault(false);
            var faultVisibility = ExecutionVisibilityReadModelBuilder.BuildFaulted(
                executionInstanceId,
                request,
                startedUtc,
                faultCompletedUtc,
                ex,
                memoryForFault);
            _visibilityStore.Put(faultVisibility);

            await _telemetry.TrackAsync(new ExecutionTelemetry(
                TenantId: request.TenantId,
                CapabilityKey: request.CapabilityKey,
                PromptKey: request.PromptKey,
                PromptVersion: null,
                PolicyKey: null,
                ProviderKey: "unknown",
                ModelKey: "unknown",
                UsedFallback: false,
                InputTokenCount: null,
                OutputTokenCount: null,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Succeeded: false,
                ErrorMessage: ex.Message,
                MemoryRequested: memoryForFault,
                MemoryItemCount: null,
                TraceThreadId: request.TraceThreadId,
                CorrelationGroupId: request.CorrelationGroupId,
                ExecutionInstanceId: executionInstanceId),
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }
}
