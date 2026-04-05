using System;
using System.Collections.Generic;
using AIL.Modules.Execution.Application;

namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>Centralized, bounded mapping for execution observability read models.</summary>
public static class ExecutionVisibilityReadModelBuilder
{
    public const int MaxOutputSummaryLength = 512;
    public const int MaxSafeErrorLength = 256;

    public static IReadOnlyList<Guid> ParseRelatedEntityIds(IReadOnlyList<string> contextReferenceIds)
    {
        var list = new List<Guid>();
        foreach (var s in contextReferenceIds)
        {
            if (Guid.TryParse(s, out var g))
                list.Add(g);
        }

        return list;
    }

    public static string SummarizeOutput(string? outputText)
    {
        var t = outputText ?? string.Empty;
        if (t.Length <= MaxOutputSummaryLength)
            return t;
        return t[..MaxOutputSummaryLength] + "…";
    }

    public static string? BoundError(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return null;
        var m = message.Trim();
        return m.Length <= MaxSafeErrorLength ? m : m[..MaxSafeErrorLength] + "…";
    }

    public static ExecutionTraceVisibility BuildTrace(
        ExecutionRequest request,
        Guid executionInstanceId) =>
        new(
            TraceThreadId: request.TraceThreadId,
            CorrelationGroupId: request.CorrelationGroupId,
            ExecutionInstanceId: executionInstanceId,
            RelatedEntityIds: ParseRelatedEntityIds(request.ContextReferenceIds),
            ChronoFlowExecutionInstanceId: request.ChronoFlowExecutionInstanceId);

    public static ExecutionReliabilityVisibility ReliabilityUnavailable() =>
        new(
            FallbackUsed: false,
            PolicyKey: null,
            StrategyKey: null,
            PrimaryProviderKey: "n/a",
            PrimaryModelKey: "n/a",
            SelectedProviderKey: "n/a",
            SelectedModelKey: "n/a",
            FallbackProviderKey: null,
            FallbackModelKey: null);

    public static ExecutionReliabilityVisibility ReliabilityFromRun(
        string policyKey,
        ProviderSelectionResult selection,
        ProviderExecutionResult result) =>
        new(
            FallbackUsed: result.UsedFallback,
            PolicyKey: policyKey,
            StrategyKey: null,
            PrimaryProviderKey: selection.PrimaryProviderKey,
            PrimaryModelKey: selection.PrimaryModelKey,
            SelectedProviderKey: result.ProviderKey,
            SelectedModelKey: result.ModelKey,
            FallbackProviderKey: selection.FallbackProviderKey,
            FallbackModelKey: selection.FallbackModelKey);

    public static ExecutionVisibilityReadModel BuildDenied(
        Guid executionInstanceId,
        ExecutionRequest request,
        DateTime startedUtc,
        DateTime completedUtc,
        string? denyReason,
        bool memoryRequested) =>
        BuildDeniedCore(request.CapabilityKey, executionInstanceId, request, startedUtc, completedUtc, denyReason, memoryRequested);

    private static ExecutionVisibilityReadModel BuildDeniedCore(
        string capabilityKey,
        Guid executionInstanceId,
        ExecutionRequest request,
        DateTime startedUtc,
        DateTime completedUtc,
        string? denyReason,
        bool memoryRequested) =>
        new(
            CapabilityKey: capabilityKey,
            Trace: BuildTrace(request, executionInstanceId),
            Prompt: new ExecutionPromptVisibility(request.PromptKey, null, false),
            Memory: new ExecutionMemoryVisibility(
                memoryRequested,
                null,
                memoryRequested ? "memory_requested_before_deny=true" : "memory_requested=false"),
            Reliability: ReliabilityUnavailable(),
            Explanation: new ExecutionExplanationVisibility(
                true,
                BoundError(denyReason) ?? "Access denied.",
                new[] { "AIL.SECURITY.DENIED" }),
            OutputSummary: string.Empty,
            StartedAtUtc: startedUtc,
            CompletedAtUtc: completedUtc,
            Status: "Denied",
            SafeErrorSummary: BoundError(denyReason));

    public static ExecutionVisibilityReadModel BuildSucceeded(
        Guid executionInstanceId,
        ExecutionRequest request,
        DateTime startedUtc,
        DateTime completedUtc,
        string policyKey,
        ProviderSelectionResult selection,
        ProviderExecutionResult providerResult,
        string promptVersion,
        bool memoryRequested,
        int? memoryItemCount,
        string outputText) =>
        new(
            CapabilityKey: request.CapabilityKey,
            Trace: BuildTrace(request, executionInstanceId),
            Prompt: new ExecutionPromptVisibility(request.PromptKey, promptVersion, true),
            Memory: new ExecutionMemoryVisibility(
                memoryRequested,
                memoryItemCount,
                memoryRequested
                    ? $"memory_requested=true;memory_items_used={memoryItemCount ?? 0}"
                    : "memory_requested=false"),
            Reliability: ReliabilityFromRun(policyKey, selection, providerResult),
            Explanation: new ExecutionExplanationVisibility(
                true,
                "Execution completed; output is summarized only in OutputSummary (no hidden reasoning stored).",
                new[] { "AIL.EXECUTION.COMPLETED" }),
            OutputSummary: SummarizeOutput(outputText),
            StartedAtUtc: startedUtc,
            CompletedAtUtc: completedUtc,
            Status: "Succeeded",
            SafeErrorSummary: null);

    public static ExecutionVisibilityReadModel BuildFaulted(
        Guid executionInstanceId,
        ExecutionRequest request,
        DateTime startedUtc,
        DateTime completedUtc,
        Exception ex,
        bool memoryRequested) =>
        new(
            CapabilityKey: request.CapabilityKey,
            Trace: BuildTrace(request, executionInstanceId),
            Prompt: new ExecutionPromptVisibility(request.PromptKey, null, false),
            Memory: new ExecutionMemoryVisibility(
                memoryRequested,
                null,
                memoryRequested ? "memory_requested_before_failure=true" : "memory_requested=false"),
            Reliability: ReliabilityUnavailable(),
            Explanation: new ExecutionExplanationVisibility(
                true,
                "Execution failed before completion; see SafeErrorSummary.",
                new[] { "AIL.EXECUTION.FAILED" }),
            OutputSummary: string.Empty,
            StartedAtUtc: startedUtc,
            CompletedAtUtc: completedUtc,
            Status: "Failed",
            SafeErrorSummary: BoundError(ex.Message));
}
