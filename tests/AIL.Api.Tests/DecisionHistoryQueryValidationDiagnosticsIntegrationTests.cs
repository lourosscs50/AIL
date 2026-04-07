using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using AIL.Api;
using AIL.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AIL.Api.Tests;

[CollectionDefinition(nameof(DecisionHistoryQueryDiagnosticsCollection), DisableParallelization = true)]
public sealed class DecisionHistoryQueryDiagnosticsCollection : ICollectionFixture<DecisionHistoryQueryDiagnosticsFactory>
{
}

/// <summary>Serializes diagnostics integration tests that share a single in-memory log sink.</summary>
public sealed class DecisionHistoryQueryDiagnosticsFactory : WebApplicationFactory<Program>
{
    public TestCapturingLoggerProvider Capture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Capture);
        });
    }
}

/// <summary>Captures formatted log lines for assertions (test support only).</summary>
public sealed class TestCapturingLoggerProvider : ILoggerProvider
{
    private readonly object _lock = new();

    public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = new();

    public void Clear()
    {
        lock (_lock)
            Entries.Clear();
    }

    public ILogger CreateLogger(string categoryName) => new EntryLogger(this);

    public void Dispose()
    {
    }

    private sealed class EntryLogger : ILogger
    {
        private readonly TestCapturingLoggerProvider _owner;

        public EntryLogger(TestCapturingLoggerProvider owner) => _owner = owner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            lock (_owner._lock)
                _owner.Entries.Add((logLevel, eventId, msg));
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

[Collection(nameof(DecisionHistoryQueryDiagnosticsCollection))]
public sealed class DecisionHistoryQueryValidationDiagnosticsIntegrationTests
{
    private readonly DecisionHistoryQueryDiagnosticsFactory _factory;

    public DecisionHistoryQueryValidationDiagnosticsIntegrationTests(DecisionHistoryQueryDiagnosticsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_InvalidTenant_EmitsInvalidTenantCategory()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/decisions/history?tenantId={Guid.Empty:D}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Contains("InvalidTenant", entry.Message, StringComparison.Ordinal);
        Assert.Contains("tenantSuppliedNonEmpty=False", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidPaging_EmitsListRejected_WithInvalidPagingCategory()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}&page=0");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("InvalidPaging", entry.Message, StringComparison.Ordinal);
        Assert.Contains("tenantSuppliedNonEmpty=True", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidDateRange_EmitsListRejected_WithInvalidDateRangeCategory()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var url =
            $"/decisions/history?tenantId={tenant:D}&fromUtc=2026-12-01T00:00:00Z&toUtc=2026-01-01T00:00:00Z";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Contains("InvalidDateRange", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedDecisionType_DiagnosticDoesNotContainFilterPayload()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var secretToken = new string('Z', 600);
        var url =
            $"/decisions/history?tenantId={tenant:D}&decisionType={Uri.EscapeDataString(secretToken)}";
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Contains("InvalidFilterShape", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretToken, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidSort_EmitsInvalidSortParametersCategory()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}&sortBy=notAllowed");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Contains("InvalidSortParameters", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyCorrelationGroupId_EmitsInvalidIdentifierValueCategory()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        var res = await client.GetAsync(
            $"/decisions/history?tenantId={tenant:D}&correlationGroupId={Guid.Empty:D}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
        Assert.Contains("InvalidIdentifierValue", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidListRequest_DoesNotEmitListRejectedDiagnostic()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var tenant = Guid.NewGuid();
        await client.PostAsJsonAsync("/decisions", new DecideRequest(
            tenant,
            "alpha_route",
            "s",
            "diag-ok",
            null,
            null,
            false,
            null,
            null,
            null,
            null));

        var res = await client.GetAsync($"/decisions/history?tenantId={tenant:D}&page=1&pageSize=10");
        res.EnsureSuccessStatusCode();

        Assert.DoesNotContain(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.ListQueryRejectedEventId.Id);
    }

    [Fact]
    public async Task Detail_InvalidTenant_EmitsDetailRejected()
    {
        _factory.Capture.Clear();
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var res = await client.GetAsync($"/decisions/history/{id:D}?tenantId={Guid.Empty:D}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var entry = Assert.Single(
            _factory.Capture.Entries,
            e => e.EventId.Id == DecisionHistoryQueryValidationDiagnostics.DetailQueryRejectedEventId.Id);
        Assert.Contains("InvalidTenant", entry.Message, StringComparison.Ordinal);
    }
}
