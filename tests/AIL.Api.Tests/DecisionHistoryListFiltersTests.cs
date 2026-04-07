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
    public void ValidateDecisionHistoryListFilters_RejectsOversizedDecisionType()
    {
        var s = new string('d', DecisionEndpointMapping.MaxDecisionHistoryDecisionTypeFilterLength + 1);
        var ex = Assert.Throws<ArgumentException>(() =>
            DecisionEndpointMapping.ValidateDecisionHistoryListFilters(null, null, null, s, null, null));
        Assert.Equal("decisionType", ex.ParamName);
    }

    [Fact]
    public void ValidateDecisionHistoryListFilters_AcceptsMaxLengthDecisionTypeFilter()
    {
        var s = new string('d', DecisionEndpointMapping.MaxDecisionHistoryDecisionTypeFilterLength);
        DecisionEndpointMapping.ValidateDecisionHistoryListFilters(null, null, null, s, null, null);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_Omitted_UsesDefaults()
    {
        Assert.True(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(null, null, out var p, out var ps, out var err));
        Assert.Null(err);
        Assert.Equal(1, p);
        Assert.Equal(DecisionEndpointMapping.DefaultDecisionHistoryListPageSize, ps);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_ExplicitValid_Accepted()
    {
        Assert.True(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(2, 100, out var p, out var ps, out var err));
        Assert.Null(err);
        Assert.Equal(2, p);
        Assert.Equal(100, ps);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_PageZero_Fails()
    {
        Assert.False(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(0, null, out _, out _, out var err));
        Assert.Equal("page must be at least 1.", err);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_PageSizeZero_Fails()
    {
        Assert.False(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(null, 0, out _, out _, out var err));
        Assert.Equal("pageSize must be at least 1.", err);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_PageSizeAboveMax_Fails()
    {
        Assert.False(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(
            1,
            DecisionEndpointMapping.MaxDecisionHistoryListPageSize + 1,
            out _,
            out _,
            out var err));
        Assert.Equal(
            $"pageSize must not exceed {DecisionEndpointMapping.MaxDecisionHistoryListPageSize}.",
            err);
    }

    [Fact]
    public void TryNormalizeDecisionHistoryListPaging_PageAboveMax_Fails()
    {
        Assert.False(DecisionEndpointMapping.TryNormalizeDecisionHistoryListPaging(
            DecisionEndpointMapping.MaxDecisionHistoryListPage + 1,
            1,
            out _,
            out _,
            out var err));
        Assert.Equal($"page must not exceed {DecisionEndpointMapping.MaxDecisionHistoryListPage}.", err);
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
