using System;
using System.Text;
using AIL.Api.Contracts;
using AIL.Modules.Execution.Application.Visibility;

namespace AIL.Api;

/// <summary>
/// Maps execution read models to the SignalForge-aligned <see cref="DecisionVisibilityResponse"/> envelope (single mapping entry point for API).
/// </summary>
internal static class DecisionVisibilityFromExecutionMapping
{
    private const int MaxNormalizedInputLength = 512;

    public static DecisionVisibilityResponse ToDecisionVisibilityResponse(ExecutionVisibilityReadModel m)
    {
        var decisionId = m.Trace.ExecutionInstanceId;
        var occurredAt = new DateTimeOffset(DateTime.SpecifyKind(m.CompletedAtUtc, DateTimeKind.Utc));

        var input = new DecisionInputSummary(NormalizeInputSummary(m));
        var output = new DecisionOutputSummary(m.OutputSummary);
        var explanation = new DecisionExplanationSummary(
            m.Explanation.ExplanationAvailable,
            m.Explanation.SummaryText,
            m.Explanation.ReasonCodes,
            ConfidenceBand: null,
            FallbackUsageCount: m.Reliability.FallbackUsed ? 1 : null,
            RetryUsageCount: null);

        var trace = new DecisionTraceSummary(
            // CorrelationId: in SignalForge this often filters by signal id; for A.I.L. we map optional CorrelationGroupId (broader workflow grouping).
            CorrelationId: m.Trace.CorrelationGroupId,
            ExecutionId: null,
            TraceId: m.Trace.TraceThreadId,
            RelatedEntityIds: m.Trace.RelatedEntityIds,
            SignalEntityId: null,
            AlertEntityId: null,
            ChronoFlowExecutionInstanceId: null,
            ExecutionInstanceId: m.Trace.ExecutionInstanceId);

        var extension = new ExecutionVisibilityExtension(
            Prompt: new ExecutionPromptFacet(
                m.Prompt.PromptKey,
                m.Prompt.PromptVersion,
                m.Prompt.ResolutionSucceeded),
            Memory: new ExecutionMemoryFacet(
                m.Memory.MemoryRequested,
                m.Memory.MemoryItemCount,
                m.Memory.ParticipationSummary),
            Reliability: new ExecutionReliabilityFacet(
                m.Reliability.FallbackUsed,
                m.Reliability.PolicyKey,
                m.Reliability.StrategyKey,
                m.Reliability.PrimaryProviderKey,
                m.Reliability.PrimaryModelKey,
                m.Reliability.SelectedProviderKey,
                m.Reliability.SelectedModelKey,
                m.Reliability.FallbackProviderKey,
                m.Reliability.FallbackModelKey),
            SafeErrorSummary: m.SafeErrorSummary,
            StartedAtUtc: m.StartedAtUtc);

        return new DecisionVisibilityResponse(
            DecisionId: decisionId,
            DecisionCategory: AilDecisionVisibilityKeys.CategoryExecution,
            DecisionType: MapDecisionType(m.Status),
            OccurredAtUtc: occurredAt,
            Status: MapVisibilityStatus(m.Status),
            Input: input,
            Output: output,
            PolicyProfileKey: m.Reliability.PolicyKey,
            StrategyPathKey: m.Reliability.StrategyKey,
            ProviderModelSummary: BuildProviderModelSummary(m),
            Explanation: explanation,
            RecommendedDownstreamSummary: null,
            AuditActorUserId: null,
            Trace: trace,
            ExecutionExtension: extension);
    }

    private static string MapVisibilityStatus(string status) =>
        status switch
        {
            "Succeeded" => AilDecisionVisibilityKeys.StatusSucceeded,
            "Denied" => AilDecisionVisibilityKeys.StatusDenied,
            "Failed" => AilDecisionVisibilityKeys.StatusFailed,
            _ => status.ToLowerInvariant()
        };

    private static string MapDecisionType(string status) =>
        status switch
        {
            "Succeeded" => AilDecisionVisibilityKeys.Types.IntelligenceSucceeded,
            "Denied" => AilDecisionVisibilityKeys.Types.IntelligenceDenied,
            "Failed" => AilDecisionVisibilityKeys.Types.IntelligenceFailed,
            _ => AilDecisionVisibilityKeys.Types.IntelligenceUnknown
        };

    private static string? BuildProviderModelSummary(ExecutionVisibilityReadModel m)
    {
        var r = m.Reliability;
        if (string.IsNullOrEmpty(r.SelectedProviderKey) || r.SelectedProviderKey == "n/a")
            return null;

        var sb = new StringBuilder();
        sb.Append(r.SelectedProviderKey).Append('/').Append(r.SelectedModelKey);
        if (r.FallbackUsed)
            sb.Append(";fallback=true");
        return sb.ToString();
    }

    private static string NormalizeInputSummary(ExecutionVisibilityReadModel m)
    {
        var s =
            $"capability={m.CapabilityKey};prompt_key={m.Prompt.PromptKey};memory_requested={m.Memory.MemoryRequested};prompt_resolved={m.Prompt.ResolutionSucceeded}";
        if (s.Length <= MaxNormalizedInputLength)
            return s;
        return s[..MaxNormalizedInputLength] + "…";
    }
}
