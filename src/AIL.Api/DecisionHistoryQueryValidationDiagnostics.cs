using System;
using Microsoft.Extensions.Logging;

namespace AIL.Api;

/// <summary>
/// Bounded, operator-safe diagnostics for rejected decision-history HTTP queries.
/// Logs only stable categories and flags—never raw query strings, filter text, or tenant identifiers.
/// </summary>
internal static class DecisionHistoryQueryValidationDiagnostics
{
    /// <summary>Event id for filtering tests and log pipelines; message carries no user-supplied text.</summary>
    internal static readonly EventId ListQueryRejectedEventId = new(9_001, "DecisionHistoryListQueryRejected");

    /// <summary>Detail GET rejected before store access (e.g. invalid tenant parameter shape).</summary>
    internal static readonly EventId DetailQueryRejectedEventId = new(9_002, "DecisionHistoryDetailQueryRejected");

    /// <summary>High-level validation failure class (no payload or user text).</summary>
    internal enum FailureCategory
    {
        InvalidTenant,
        InvalidDateRange,
        InvalidPaging,
        InvalidFilterShape,
        InvalidIdentifierValue,
        InvalidSortParameters,
    }

    internal static FailureCategory FailureCategoryFromFilterValidationException(ArgumentException ex) =>
        ex.ParamName switch
        {
            "correlationGroupId" or "executionInstanceId" => FailureCategory.InvalidIdentifierValue,
            "memoryInfluenceSummary" or "decisionType" or "selectedStrategyKey" or "policyKey" =>
                FailureCategory.InvalidFilterShape,
            _ => FailureCategory.InvalidFilterShape,
        };

    internal static void LogListRejected(
        ILogger logger,
        FailureCategory category,
        bool tenantSuppliedNonEmpty) =>
        logger.Log(
            LogLevel.Warning,
            ListQueryRejectedEventId,
            "DecisionHistory list query rejected: category={Category}, tenantSuppliedNonEmpty={TenantSuppliedNonEmpty}",
            category,
            tenantSuppliedNonEmpty);

    internal static void LogDetailRejected(
        ILogger logger,
        FailureCategory category,
        bool tenantSuppliedNonEmpty) =>
        logger.Log(
            LogLevel.Warning,
            DetailQueryRejectedEventId,
            "DecisionHistory detail query rejected: category={Category}, tenantSuppliedNonEmpty={TenantSuppliedNonEmpty}",
            category,
            tenantSuppliedNonEmpty);
}
