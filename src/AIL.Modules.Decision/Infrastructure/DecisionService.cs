using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.MemoryCore.Application;
using AIL.Modules.Observability.Application;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Decision.Infrastructure;

/// <summary>
/// Orchestrates the decision pipeline. Precedence is explicit and fixed:
/// <list type="number">
/// <item><description><b>Winner</b> — chosen only from strategy evaluations by deterministic score, then
/// <see cref="DecisionStrategyEvaluation.SuggestedStrategyKey"/>, then registry key. <see cref="DecisionPolicy"/> is not used here.</description></item>
/// <item><description><b>Options list</b> — every evaluation is projected to an option, then filtered by
/// <see cref="DecisionPolicy.MinimumConfidence"/> and capped by <see cref="DecisionPolicy.MaxOptions"/> only.</description></item>
/// <item><description><b>Winner fallback options</b> — if policy filtering removes every option, a single option is built
/// from the winner (same id, confidence, strength, rationale as the winning evaluation). Deterministic and repeatable.</description></item>
/// </list>
/// </summary>
internal sealed class DecisionService : IDecisionService
{
    private readonly IMemoryService _memory;
    private readonly IReadOnlyList<IDecisionStrategy> _strategies;
    private readonly IDecisionPolicyService _policyService;
    private readonly IDecisionTelemetryService _telemetry;

    public DecisionService(
        IMemoryService memoryService,
        IEnumerable<IDecisionStrategy> strategies,
        IDecisionPolicyService policyService,
        IDecisionTelemetryService telemetry)
    {
        _memory = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
        _strategies = strategies?.OrderBy(s => s.StrategyKey, StringComparer.Ordinal).ToList()
            ?? throw new ArgumentNullException(nameof(strategies));
    }

    public async Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        string? policyKey = null;

        try
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            Validate(request);

            var policy = await _policyService.ResolvePolicyAsync(request.DecisionType, cancellationToken).ConfigureAwait(false);
            policyKey = policy.PolicyKey;

            DecisionMemoryContext? memory = null;
            var usedMemory = false;
            var memoryItemCount = 0;

            if (request.IncludeMemory)
            {
                memory = await DecisionMemoryLoader.LoadAsync(
                    _memory,
                    request.TenantId,
                    request.MemoryQuery!,
                    cancellationToken).ConfigureAwait(false);
                usedMemory = true;
                memoryItemCount = memory.Items.Count;
            }

            var evaluated = new List<(string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal)>();
            foreach (var strategy in _strategies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!strategy.CanHandle(request, memory))
                    continue;

                var eval = strategy.Evaluate(request, memory);
                var signal = ExtractDecisionSignal(strategy.StrategyKey, eval);
                evaluated.Add((strategy.StrategyKey, eval, signal));
            }

            if (evaluated.Count == 0)
                throw new InvalidOperationException("No decision strategy applied.");

            // Winner: score ordering and tie-breakers only — policy never vetoes or reshapes this selection.
            var winner = SelectWinnerByScore(evaluated);

            var considered = evaluated
                .Select(x => x.RegistryKey)
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            var activeSignals = evaluated
                .Select(x => x.Signal)
                .Where(s => s.IsActive)
                .ToList();

            if (activeSignals.Any(s => s.Type != DecisionSignalType.DefaultFallback))
            {
                activeSignals = activeSignals
                    .Where(s => s.Type != DecisionSignalType.DefaultFallback)
                    .ToList();
            }

            var options = BuildPolicyFilteredOptions(evaluated, policy);
            if (options.Count == 0)
                options = BuildWinnerFallbackOptionsList(winner);

            var memoryInfluenceSummary = MemoryInfluenceSummaryResolver.Resolve(
                usedMemory,
                memoryItemCount,
                winner,
                evaluated);

            var result = new DecisionResult(
                DecisionType: request.DecisionType,
                SelectedStrategyKey: winner.Eval.SuggestedStrategyKey,
                Confidence: DecisionConfidenceMapper.FromScore(winner.Eval.DeterministicScore),
                ReasonSummary: DecisionExplanationBuilder.BuildExplanation(activeSignals),
                ConsideredStrategies: considered,
                UsedMemory: usedMemory,
                MemoryItemCount: memoryItemCount,
                MemoryInfluenceSummary: memoryInfluenceSummary,
                Options: options,
                PolicyKey: policy.PolicyKey,
                Metadata: request.Metadata);

            stopwatch.Stop();
            await _telemetry.TrackAsync(
                new DecisionTelemetry(
                    TenantId: request.TenantId,
                    DecisionType: request.DecisionType,
                    SelectedStrategyKey: winner.Eval.SuggestedStrategyKey,
                    UsedMemory: usedMemory,
                    MemoryItemCount: memoryItemCount,
                    CandidateStrategyCount: request.CandidateStrategies?.Count ?? 0,
                    ConsideredStrategyCount: considered.Count,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Succeeded: true,
                    PolicyKey: policy.PolicyKey,
                    MemoryInfluenceSummary: memoryInfluenceSummary),
                cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _telemetry.TrackAsync(
                new DecisionTelemetry(
                    TenantId: request?.TenantId ?? Guid.Empty,
                    DecisionType: request?.DecisionType ?? string.Empty,
                    SelectedStrategyKey: "none",
                    UsedMemory: request?.IncludeMemory ?? false,
                    MemoryItemCount: 0,
                    CandidateStrategyCount: request?.CandidateStrategies?.Count ?? 0,
                    ConsideredStrategyCount: 0,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Succeeded: false,
                    PolicyKey: policyKey,
                    ErrorMessage: ex.Message),
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    private static (string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal) SelectWinnerByScore(
        List<(string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal)> evaluated) =>
        evaluated
            .OrderByDescending(x => x.Eval.DeterministicScore)
            .ThenBy(x => x.Eval.SuggestedStrategyKey, StringComparer.Ordinal)
            .ThenBy(x => x.RegistryKey, StringComparer.Ordinal)
            .First();

    /// <summary>
    /// Policy applies only here: confidence floor and max count. Does not change the winner.
    /// </summary>
    private static List<DecisionOption> BuildPolicyFilteredOptions(
        IReadOnlyList<(string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal)> evaluated,
        DecisionPolicy policy) =>
        evaluated
            .Select(x => CreateDecisionOption(
                x.Eval.SuggestedStrategyKey,
                DecisionConfidenceMapper.FromScore(x.Eval.DeterministicScore),
                NormalizeStrength(x.Eval.DeterministicScore),
                DecisionExplanationBuilder.BuildExplanation(new[] { x.Signal })))
            .Where(o => o.Confidence >= policy.MinimumConfidence)
            .OrderByDescending(o => o.Strength)
            .ThenBy(o => o.OptionId, StringComparer.Ordinal)
            .Take(policy.MaxOptions)
            .ToList();

    /// <summary>
    /// First-class fallback when <see cref="BuildPolicyFilteredOptions"/> yields no rows: reintroduce exactly the winner as the sole option.
    /// </summary>
    private static List<DecisionOption> BuildWinnerFallbackOptionsList(
        (string RegistryKey, DecisionStrategyEvaluation Eval, DecisionSignal Signal) winner) =>
        new()
        {
            new DecisionOption(
                OptionId: winner.Eval.SuggestedStrategyKey,
                Confidence: DecisionConfidenceMapper.FromScore(winner.Eval.DeterministicScore),
                Strength: NormalizeStrength(winner.Eval.DeterministicScore),
                RationaleSummary: DecisionExplanationBuilder.BuildExplanation(new[] { winner.Signal })),
        };

    private static double NormalizeStrength(int deterministicScore) =>
        Math.Clamp(deterministicScore / 1000.0, 0.0, 1.0);

    private static DecisionOption CreateDecisionOption(string optionId, DecisionConfidence confidence, double strength, string rationaleSummary)
    {
        if (string.IsNullOrWhiteSpace(optionId))
            throw new ArgumentException("OptionId is required.", nameof(optionId));

        if (strength < 0.0 || strength > 1.0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be between 0.0 and 1.0.");

        if (string.IsNullOrWhiteSpace(rationaleSummary))
            throw new ArgumentException("RationaleSummary is required.", nameof(rationaleSummary));

        return new DecisionOption(optionId, confidence, strength, rationaleSummary);
    }

    private static DecisionSignal ExtractDecisionSignal(string strategyKey, DecisionStrategyEvaluation eval)
    {
        var isActive = eval.DeterministicScore > 0;

        return strategyKey switch
        {
            KnownDecisionStrategyKeys.CandidateMatch => new DecisionSignal(DecisionSignalType.CandidateMatch, 100, isActive),
            KnownDecisionStrategyKeys.ContextEscalated => new DecisionSignal(DecisionSignalType.EscalationSignal, 90, isActive),
            KnownDecisionStrategyKeys.MemoryInformed => new DecisionSignal(DecisionSignalType.MemoryContext, 70, isActive),
            KnownDecisionStrategyKeys.DecisionContinuity => new DecisionSignal(DecisionSignalType.HistoricalContinuity, 80, isActive),
            KnownDecisionStrategyKeys.DefaultSafe => new DecisionSignal(DecisionSignalType.DefaultFallback, 10, isActive),
            _ => new DecisionSignal(DecisionSignalType.Unknown, 0, isActive)
        };
    }

    private static void Validate(DecisionRequest request)
    {
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.DecisionType))
            throw new ArgumentException("DecisionType is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.SubjectType))
            throw new ArgumentException("SubjectType is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.SubjectId))
            throw new ArgumentException("SubjectId is required.", nameof(request));

        if (request.IncludeMemory && request.MemoryQuery is null)
            throw new ArgumentException("MemoryQuery is required when IncludeMemory is true.", nameof(request));
    }
}
