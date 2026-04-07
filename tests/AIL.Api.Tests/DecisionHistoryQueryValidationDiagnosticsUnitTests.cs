using System;
using System.Collections.Generic;
using AIL.Api;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AIL.Api.Tests;

public sealed class DecisionHistoryQueryValidationDiagnosticsUnitTests
{
    [Fact]
    public void FailureCategoryFromFilterValidationException_CorrelationGroupId_IsInvalidIdentifierValue()
    {
        var ex = new ArgumentException("x", "correlationGroupId");
        Assert.Equal(
            DecisionHistoryQueryValidationDiagnostics.FailureCategory.InvalidIdentifierValue,
            DecisionHistoryQueryValidationDiagnostics.FailureCategoryFromFilterValidationException(ex));
    }

    [Fact]
    public void FailureCategoryFromFilterValidationException_ExecutionInstanceId_IsInvalidIdentifierValue()
    {
        var ex = new ArgumentException("x", "executionInstanceId");
        Assert.Equal(
            DecisionHistoryQueryValidationDiagnostics.FailureCategory.InvalidIdentifierValue,
            DecisionHistoryQueryValidationDiagnostics.FailureCategoryFromFilterValidationException(ex));
    }

    [Fact]
    public void FailureCategoryFromFilterValidationException_StringFilters_MapToInvalidFilterShape()
    {
        foreach (var p in new[] { "memoryInfluenceSummary", "decisionType", "selectedStrategyKey", "policyKey" })
        {
            var ex = new ArgumentException("x", p);
            Assert.Equal(
                DecisionHistoryQueryValidationDiagnostics.FailureCategory.InvalidFilterShape,
                DecisionHistoryQueryValidationDiagnostics.FailureCategoryFromFilterValidationException(ex));
        }
    }

    [Fact]
    public void FailureCategoryFromFilterValidationException_MissingParamName_DefaultsToInvalidFilterShape()
    {
        var ex = new ArgumentException("x");
        Assert.Equal(
            DecisionHistoryQueryValidationDiagnostics.FailureCategory.InvalidFilterShape,
            DecisionHistoryQueryValidationDiagnostics.FailureCategoryFromFilterValidationException(ex));
    }

    [Fact]
    public void LogListRejected_FormattedMessage_IsCategoryOnly_NoUserPayload()
    {
        var provider = new UnitTestListCaptureLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var logger = factory.CreateLogger<Program>();

        DecisionHistoryQueryValidationDiagnostics.LogListRejected(
            logger,
            DecisionHistoryQueryValidationDiagnostics.FailureCategory.InvalidFilterShape,
            tenantSuppliedNonEmpty: true);

        var entry = Assert.Single(provider.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId, entry.EventId);
        Assert.Contains("InvalidFilterShape", entry.Message, StringComparison.Ordinal);
        Assert.Contains("tenantSuppliedNonEmpty=True", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USER_SHOULD_NEVER_APPEAR", entry.Message, StringComparison.Ordinal);
    }

    private sealed class UnitTestListCaptureLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new L(this);

        public void Dispose()
        {
        }

        private sealed class L : ILogger
        {
            private readonly UnitTestListCaptureLoggerProvider _owner;

            public L(UnitTestListCaptureLoggerProvider owner) => _owner = owner;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _owner.Entries.Add((logLevel, eventId, formatter(state, exception)));
            }
        }

        private sealed class NoopScope : IDisposable
        {
            internal static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
