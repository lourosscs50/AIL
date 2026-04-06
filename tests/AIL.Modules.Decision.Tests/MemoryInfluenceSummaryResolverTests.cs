using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Domain;
using AIL.Modules.Decision.Infrastructure;

namespace AIL.Modules.Decision.Tests;

public sealed class MemoryInfluenceSummaryResolverTests
{
    private static DecisionSignal Sig(DecisionSignalType t, int s = 50, bool active = true) =>
        new(t, s, active);

    [Fact]
    public void Resolve_ReturnsNoMemory_WhenMemoryNotUsed()
    {
        var w = (KnownDecisionStrategyKeys.DefaultSafe,
            new DecisionStrategyEvaluation(100, KnownDecisionStrategyKeys.DefaultSafe, "r"),
            Sig(DecisionSignalType.DefaultFallback));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)> { w };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.NoMemory,
            MemoryInfluenceSummaryResolver.Resolve(false, 0, w, ev));
    }

    [Fact]
    public void Resolve_ReturnsMemoryEmpty_WhenNoItems()
    {
        var w = (KnownDecisionStrategyKeys.DefaultSafe,
            new DecisionStrategyEvaluation(100, KnownDecisionStrategyKeys.DefaultSafe, "r"),
            Sig(DecisionSignalType.DefaultFallback));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)> { w };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.MemoryEmpty,
            MemoryInfluenceSummaryResolver.Resolve(true, 0, w, ev));
    }

    [Fact]
    public void Resolve_ReturnsMemoryReinforced_WhenMemoryInformedWins()
    {
        var w = (KnownDecisionStrategyKeys.MemoryInformed,
            new DecisionStrategyEvaluation(800, KnownDecisionStrategyKeys.MemoryInformed, "r"),
            Sig(DecisionSignalType.MemoryContext));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)>
        {
            w,
            (KnownDecisionStrategyKeys.DefaultSafe,
                new DecisionStrategyEvaluation(100, KnownDecisionStrategyKeys.DefaultSafe, "r"),
                Sig(DecisionSignalType.DefaultFallback)),
        };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.MemoryReinforced,
            MemoryInfluenceSummaryResolver.Resolve(true, 2, w, ev));
    }

    [Fact]
    public void Resolve_ReturnsMemoryConsistent_WhenContinuityWins()
    {
        var w = (KnownDecisionStrategyKeys.DecisionContinuity,
            new DecisionStrategyEvaluation(100, "historical_route", "r"),
            Sig(DecisionSignalType.HistoricalContinuity));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)>
        {
            w,
            (KnownDecisionStrategyKeys.DefaultSafe,
                new DecisionStrategyEvaluation(100, KnownDecisionStrategyKeys.DefaultSafe, "r"),
                Sig(DecisionSignalType.DefaultFallback)),
        };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.MemoryConsistent,
            MemoryInfluenceSummaryResolver.Resolve(true, 1, w, ev));
    }

    [Fact]
    public void Resolve_ReturnsMemoryConflict_WhenMemoryStrategiesDisagree()
    {
        var winner = (KnownDecisionStrategyKeys.MemoryInformed,
            new DecisionStrategyEvaluation(820, KnownDecisionStrategyKeys.MemoryInformed, "r"),
            Sig(DecisionSignalType.MemoryContext));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)>
        {
            winner,
            (KnownDecisionStrategyKeys.DecisionContinuity,
                new DecisionStrategyEvaluation(100, "other_route", "r"),
                Sig(DecisionSignalType.HistoricalContinuity)),
            (KnownDecisionStrategyKeys.DefaultSafe,
                new DecisionStrategyEvaluation(100, KnownDecisionStrategyKeys.DefaultSafe, "r"),
                Sig(DecisionSignalType.DefaultFallback)),
        };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.MemoryConflict,
            MemoryInfluenceSummaryResolver.Resolve(true, 2, winner, ev));
    }

    [Fact]
    public void Resolve_ReturnsMemoryNeutral_WhenStrongerNonMemoryWins()
    {
        var winner = (KnownDecisionStrategyKeys.CandidateMatch,
            new DecisionStrategyEvaluation(1000, "route", "r"),
            Sig(DecisionSignalType.CandidateMatch));
        var ev = new List<(string, DecisionStrategyEvaluation, DecisionSignal)>
        {
            winner,
            (KnownDecisionStrategyKeys.MemoryInformed,
                new DecisionStrategyEvaluation(800, KnownDecisionStrategyKeys.MemoryInformed, "r"),
                Sig(DecisionSignalType.MemoryContext)),
        };

        Assert.Equal(
            KnownMemoryInfluenceSummaries.MemoryNeutral,
            MemoryInfluenceSummaryResolver.Resolve(true, 1, winner, ev));
    }
}
