using System.Linq;
using AIL.Api;
using AIL.Api.Contracts;
using AIL.Modules.Audit.Infrastructure;
using AIL.Modules.ContextEngine.Infrastructure;
using AIL.Modules.Decision.Application;
using AIL.Modules.Decision.Infrastructure;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Infrastructure;
using AIL.Modules.MemoryCore.Infrastructure;
using AIL.Modules.Observability.Infrastructure;
using AIL.Modules.PolicyRegistry.Infrastructure;
using AIL.Modules.PromptRegistry.Infrastructure;
using AIL.Modules.ProviderRegistry.Infrastructure;
using AIL.Modules.Security.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSecurityModule();
builder.Services.AddPromptRegistryModule();
builder.Services.AddPolicyRegistryModule();
builder.Services.AddContextEngineModule();
builder.Services.AddAuditModule();
builder.Services.AddObservabilityModule();
builder.Services.AddProviderRegistryModule();
builder.Services.AddMemoryCoreModule();
builder.Services.AddDecisionModule(builder.Configuration);
builder.Services.AddExecutionModule(builder.Configuration);

var app = builder.Build();

app.MapPost("/execute-intelligence", async (ExecuteIntelligenceRequest request, HttpContext http, IExecutionService execution) =>
{
    ExecutionMemoryQuery? memoryQuery = null;
    if (request.IncludeMemory && request.MemoryQuery is not null)
    {
        var mq = request.MemoryQuery;
        IReadOnlyList<string>? keys = mq.Keys is { Count: > 0 } ? mq.Keys : null;
        memoryQuery = new ExecutionMemoryQuery(
            ScopeType: mq.ScopeType,
            ScopeId: mq.ScopeId,
            MemoryKind: mq.MemoryKind,
            Keys: keys,
            TakeRecent: mq.TakeRecent,
            IncludeMetadata: mq.IncludeMetadata);
    }

    var result = await execution.ExecuteAsync(new ExecutionRequest(
        TenantId: request.TenantId,
        CapabilityKey: request.CapabilityKey,
        PromptKey: request.PromptKey,
        Variables: request.Variables ?? new Dictionary<string, string>(),
        ContextReferenceIds: request.ContextReferenceIds ?? new List<string>(),
        IncludeMemory: request.IncludeMemory,
        MemoryQuery: memoryQuery,
        ExecutionInstanceId: request.ExecutionInstanceId,
        TraceThreadId: request.TraceThreadId,
        CorrelationGroupId: request.CorrelationGroupId,
        ChronoFlowExecutionInstanceId: request.ChronoFlowExecutionInstanceId));

    if (!result.IsAllowed)
    {
        http.Response.Headers.Append("X-Ail-Execution-Instance-Id", result.ExecutionInstanceId.ToString());
        // Forbid() triggers auth middleware; use explicit 403 to avoid requiring authentication services.
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Ok(new ExecuteIntelligenceResponse(
        OutputText: result.OutputText,
        ProviderKey: result.ProviderKey,
        ModelKey: result.ModelKey,
        PromptVersion: result.PromptVersion,
        AuditRecordId: result.AuditRecordId,
        ExecutionInstanceId: result.ExecutionInstanceId,
        DecisionVisibility: DecisionVisibilityFromExecutionMapping.ToDecisionVisibilityResponse(result.Visibility)));
});

app.MapGet("/executions", (int? page, int? pageSize, IExecutionVisibilityReadStore store) =>
{
    var p = page is >= 1 ? page.Value : 1;
    var ps = pageSize is >= 1 && pageSize <= 100 ? pageSize.Value : 50;
    var (models, total) = store.ListByCompletedAtDescending(p, ps);
    var items = models.Select(DecisionVisibilityFromExecutionMapping.ToDecisionVisibilityResponse).ToList();
    return Results.Ok(new PagedDecisionVisibilityResponse(items, p, ps, total));
});

app.MapGet("/executions/{id:guid}", (Guid id, IExecutionVisibilityReadStore store) =>
{
    var model = store.TryGet(id);
    return model is null
        ? Results.NotFound()
        : Results.Ok(DecisionVisibilityFromExecutionMapping.ToDecisionVisibilityResponse(model));
});

app.MapPost("/decisions", async (
    DecideRequest request,
    IDecisionService decision,
    IDecisionHistoryRecorder historyRecorder,
    CancellationToken ct) =>
{
    try
    {
        var decisionRequest = DecisionEndpointMapping.MapToDecisionRequest(request);
        var result = await decision.DecideAsync(decisionRequest, ct).ConfigureAwait(false);
        var recordId = historyRecorder.TryRecord(decisionRequest, result);
        return Results.Ok(DecisionEndpointMapping.MapToDecideResponse(
            result,
            recordId,
            decisionRequest.CorrelationGroupId,
            decisionRequest.ExecutionInstanceId));
    }
    catch (Exception ex) when (ex is ArgumentException or ArgumentNullException or InvalidOperationException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/decisions/history/{id:guid}", (Guid id, Guid tenantId, IDecisionHistoryStore store) =>
{
    if (tenantId == Guid.Empty)
        return Results.BadRequest(new { error = "tenantId is required." });

    var record = store.TryGet(tenantId, id);
    return record is null
        ? Results.NotFound()
        : Results.Ok(DecisionHistoryEndpointMapping.ToItemResponse(record));
});

app.MapGet("/decisions/history", (
    Guid tenantId,
    int? page,
    int? pageSize,
    string? decisionType,
    string? selectedStrategyKey,
    string? policyKey,
    DateTime? fromUtc,
    DateTime? toUtc,
    Guid? correlationGroupId,
    string? memoryInfluenceSummary,
    Guid? executionInstanceId,
    string? sortBy,
    string? sortDirection,
    IDecisionHistoryStore store) =>
{
    if (tenantId == Guid.Empty)
        return Results.BadRequest(new { error = "tenantId is required." });

    if (fromUtc is DateTime f && toUtc is DateTime t && f > t)
        return Results.BadRequest(new { error = "fromUtc must not be after toUtc." });

    try
    {
        DecisionEndpointMapping.ValidateDecisionHistoryListFilters(
            correlationGroupId,
            memoryInfluenceSummary,
            executionInstanceId);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    DecisionHistorySortBy sortByEnum;
    DecisionHistorySortDirection sortDirEnum;
    try
    {
        sortByEnum = DecisionEndpointMapping.ParseDecisionHistorySortBy(sortBy);
        sortDirEnum = DecisionEndpointMapping.ParseDecisionHistorySortDirection(sortDirection);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var p = page is >= 1 ? page.Value : 1;
    var ps = pageSize is >= 1 && pageSize <= 100 ? pageSize.Value : 50;
    var query = DecisionEndpointMapping.CreateDecisionHistoryListQuery(
        tenantId,
        p,
        ps,
        string.IsNullOrWhiteSpace(decisionType) ? null : decisionType,
        string.IsNullOrWhiteSpace(selectedStrategyKey) ? null : selectedStrategyKey,
        string.IsNullOrWhiteSpace(policyKey) ? null : policyKey,
        fromUtc,
        toUtc,
        correlationGroupId,
        string.IsNullOrWhiteSpace(memoryInfluenceSummary) ? null : memoryInfluenceSummary.Trim(),
        executionInstanceId,
        sortByEnum,
        sortDirEnum);

    var (items, total) = store.List(query);
    var dto = items.Select(DecisionHistoryEndpointMapping.ToListItemResponse).ToList();
    return Results.Ok(new PagedDecisionHistoryResponse(
        dto,
        p,
        ps,
        total,
        DecisionEndpointMapping.ToDecisionHistorySortByApiValue(sortByEnum),
        DecisionEndpointMapping.ToDecisionHistorySortDirectionApiValue(sortDirEnum)));
});

app.Run();

public partial class Program { }
