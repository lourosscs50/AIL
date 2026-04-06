using System;
using AIL.Modules.Decision.Infrastructure;
using Xunit;

namespace AIL.Modules.Decision.Tests;

public sealed class DecisionHistoryPersistenceValidatorTests
{
    [Theory]
    [InlineData("Data Source=test.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Filename=test.db")]
    public void ValidateAndNormalizeSqliteConnectionString_AcceptsWellFormedStrings(string conn)
    {
        var normalized = DecisionHistoryPersistenceValidator.ValidateAndNormalizeSqliteConnectionString(conn);
        Assert.Equal(conn.Trim(), normalized);
    }

    [Fact]
    public void ValidateAndNormalizeSqliteConnectionString_RejectsNullOrWhitespace()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DecisionHistoryPersistenceValidator.ValidateAndNormalizeSqliteConnectionString(null));
        Assert.Throws<InvalidOperationException>(() =>
            DecisionHistoryPersistenceValidator.ValidateAndNormalizeSqliteConnectionString("   "));
    }

    [Fact]
    public void ValidateAndNormalizeSqliteConnectionString_RejectsEmptyDataSource()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DecisionHistoryPersistenceValidator.ValidateAndNormalizeSqliteConnectionString("Data Source="));
        Assert.Contains("data source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
