using System;
using AIL.Api;
using Xunit;

namespace AIL.Api.Tests;

public sealed class DecisionHistoryListFiltersTests
{
    [Fact]
    public void ValidateDecisionHistoryListFilters_AcceptsNulls()
    {
        DecisionEndpointMapping.ValidateDecisionHistoryListFilters(null, null);
    }

    [Fact]
    public void ValidateDecisionHistoryListFilters_RejectsEmptyCorrelation()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DecisionEndpointMapping.ValidateDecisionHistoryListFilters(Guid.Empty, null));
        Assert.Equal("correlationGroupId", ex.ParamName);
    }

    [Fact]
    public void ValidateDecisionHistoryListFilters_AcceptsMaxLengthMemorySummary()
    {
        var s = new string('m', DecisionEndpointMapping.MaxMemoryInfluenceSummaryFilterLength);
        DecisionEndpointMapping.ValidateDecisionHistoryListFilters(null, s);
    }
}
