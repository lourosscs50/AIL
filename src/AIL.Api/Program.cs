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
builder.Services.AddDecisionModule();
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

app.MapPost("/decisions", async (DecideRequest request, IDecisionService decision, CancellationToken ct) =>
{
    try
    {
        var decisionRequest = DecisionEndpointMapping.MapToDecisionRequest(request);
        var result = await decision.DecideAsync(decisionRequest, ct).ConfigureAwait(false);
        return Results.Ok(DecisionEndpointMapping.MapToDecideResponse(result));
    }
    catch (Exception ex) when (ex is ArgumentException or ArgumentNullException or InvalidOperationException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public partial class Program { }

file static class DecisionEndpointMapping
{
    public static DecisionRequest MapToDecisionRequest(DecideRequest req)
    {
        DecisionMemoryQuery? memoryQuery = null;
        if (req.IncludeMemory && req.MemoryQuery is not null)
        {
            var mq = req.MemoryQuery;
            IReadOnlyList<string>? keys = mq.Keys is { Count: > 0 } ? mq.Keys : null;
            memoryQuery = new DecisionMemoryQuery(
                ScopeType: mq.ScopeType,
                ScopeId: mq.ScopeId,
                MemoryKind: mq.MemoryKind,
                Keys: keys,
                TakeRecent: mq.TakeRecent,
                IncludeMetadata: mq.IncludeMetadata);
        }

        return new DecisionRequest(
            TenantId: req.TenantId,
            DecisionType: req.DecisionType,
            SubjectType: req.SubjectType,
            SubjectId: req.SubjectId,
            ContextText: req.ContextText,
            StructuredContext: req.StructuredContext,
            IncludeMemory: req.IncludeMemory,
            MemoryQuery: memoryQuery,
            CandidateStrategies: req.CandidateStrategies,
            Metadata: req.Metadata);
    }

    public static DecideResponse MapToDecideResponse(DecisionResult result) =>
        new(
            DecisionType: result.DecisionType,
            SelectedStrategyKey: result.SelectedStrategyKey,
            Confidence: result.Confidence.ToString(),
            ReasonSummary: result.ReasonSummary,
            Options: result.Options.Select(o => new DecideOptionResponse(
                OptionId: o.OptionId,
                Confidence: o.Confidence.ToString(),
                Strength: o.Strength,
                RationaleSummary: o.RationaleSummary)).ToList(),
            ConsideredStrategies: result.ConsideredStrategies,
            UsedMemory: result.UsedMemory,
            MemoryItemCount: result.MemoryItemCount,
            Metadata: result.Metadata);
}
