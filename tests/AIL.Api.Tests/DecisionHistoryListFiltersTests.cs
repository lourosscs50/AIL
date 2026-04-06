using System;
using AIL.Api;
using AIL.Modules.Decision.Application;
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

    [Fact]
    public void ValidateDecisionHistoryListFilters_RejectsEmptyExecutionInstanceId()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DecisionEndpointMapping.ValidateDecisionHistoryListFilters(null, null, Guid.Empty));
        Assert.Equal("executionInstanceId", ex.ParamName);
    }

    [Fact]
    public void ParseDecisionHistorySortBy_DefaultsToCreatedAtUtc()
    {
        Assert.Equal(DecisionHistorySortBy.CreatedAtUtc, DecisionEndpointMapping.ParseDecisionHistorySortBy(null));
        Assert.Equal(
            DecisionHistorySortBy.CreatedAtUtc,
            DecisionEndpointMapping.ParseDecisionHistorySortBy("  createdAtUtc  "));
    }

    [Fact]
    public void ParseDecisionHistorySortBy_Unknown_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => DecisionEndpointMapping.ParseDecisionHistorySortBy("policyKey"));
        Assert.Equal("sortBy", ex.ParamName);
    }

    [Fact]
    public void ParseDecisionHistorySortDirection_DefaultsToDescending()
    {
        Assert.Equal(
            DecisionHistorySortDirection.Descending,
            DecisionEndpointMapping.ParseDecisionHistorySortDirection(null));
    }

    [Fact]
    public void ParseDecisionHistorySortDirection_AscDesc_AreCaseInsensitive()
    {
        Assert.Equal(
            DecisionHistorySortDirection.Ascending,
            DecisionEndpointMapping.ParseDecisionHistorySortDirection("ASC"));
        Assert.Equal(
            DecisionHistorySortDirection.Descending,
            DecisionEndpointMapping.ParseDecisionHistorySortDirection("Desc"));
    }

    [Fact]
    public void ParseDecisionHistorySortDirection_Invalid_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DecisionEndpointMapping.ParseDecisionHistorySortDirection("newest"));
        Assert.Equal("sortDirection", ex.ParamName);
    }
}
