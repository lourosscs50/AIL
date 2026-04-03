using AIL.Modules.Execution.Application;
using AIL.Modules.Security.Application;
using AIL.Modules.ContextEngine.Application;
using AIL.Modules.PolicyRegistry.Application;
using AIL.Modules.PromptRegistry.Application;
using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;
using AIL.Modules.Security.Domain;
using AIL.Modules.Observability.Application;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Infrastructure;

internal sealed class ExecutionService : IExecutionService
{
    private readonly ISecurityService _security;
    private readonly IPromptRegistryService _promptRegistry;
    private readonly IPolicyRegistryService _policyRegistry;
    private readonly IProviderSelectionService _selection;
    private readonly IContextEngineService _contextEngine;
    private readonly IExecutionReliabilityService _reliability;
    private readonly IAuditService _audit;
    private readonly IExecutionTelemetryService _telemetry;

    public ExecutionService(
        ISecurityService security,
        IPromptRegistryService promptRegistry,
        IPolicyRegistryService policyRegistry,
        IProviderSelectionService selection,
        IContextEngineService contextEngine,
        IExecutionReliabilityService reliability,
        IAuditService audit,
        IExecutionTelemetryService telemetry)
    {
        _security = security;
        _promptRegistry = promptRegistry;
        _policyRegistry = policyRegistry;
        _selection = selection;
        _contextEngine = contextEngine;
        _reliability = reliability;
        _audit = audit;
        _telemetry = telemetry;
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tenantId = request.TenantId == Guid.Empty
                ? null
                : new TenantId(request.TenantId.ToString());

            var accessDecision = await _security.EvaluateAccessAsync(tenantId, cancellationToken);
            if (!accessDecision.IsAllowed)
            {
                stopwatch.Stop();

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
                    ErrorMessage: "Access denied"),
                    cancellationToken);

                return new ExecutionResult(
                    IsAllowed: false,
                    DenyReason: accessDecision.Reason,
                    OutputText: string.Empty,
                    ProviderKey: string.Empty,
                    ModelKey: string.Empty,
                    PromptVersion: string.Empty,
                    AuditRecordId: denialAuditId);
            }

            var prompt = await _promptRegistry.ResolvePromptAsync(request.PromptKey, null, request.Variables, cancellationToken);
            var policy = await _policyRegistry.ResolvePolicyAsync(request.CapabilityKey, cancellationToken);
var selection = await _selection.SelectAsync(request.CapabilityKey, cancellationToken);
var context = await _contextEngine.BuildContextAsync(request.ContextReferenceIds, request.Variables, cancellationToken);

var contextText =
    $"refs=[{string.Join(",", context.ReferenceIds)}] vars=[{string.Join(",", context.Variables.Keys.OrderBy(key => key))}]";

var providerStopwatch = Stopwatch.StartNew();

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
        Metadata: new Dictionary<string, string>
        {
            ["CapabilityKey"] = request.CapabilityKey,
            ["PromptKey"] = request.PromptKey,
            ["PolicyKey"] = policy.PolicyKey,
            ["PrimaryProviderKey"] = selection.PrimaryProviderKey,
            ["PrimaryModelKey"] = selection.PrimaryModelKey,
            ["FallbackProviderKey"] = selection.FallbackProviderKey ?? string.Empty,
            ["FallbackModelKey"] = selection.FallbackModelKey ?? string.Empty,
            ["FallbackAllowed"] = selection.FallbackAllowed.ToString()
        }),
    cancellationToken);

providerStopwatch.Stop();

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
                Notes: $"Provider call took {providerStopwatch.ElapsedMilliseconds}ms"),
                cancellationToken);

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
                Succeeded: true),
                cancellationToken);

            return new ExecutionResult(
                IsAllowed: true,
                DenyReason: null,
                OutputText: providerResult.OutputText,
                ProviderKey: providerResult.ProviderKey,
                ModelKey: providerResult.ModelKey,
                PromptVersion: prompt.Version,
                AuditRecordId: auditId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

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
                ErrorMessage: ex.Message),
                cancellationToken);

            throw;
        }
    }
}
