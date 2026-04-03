using AIL.Api.Contracts;
using AIL.Modules.Audit.Infrastructure;
using AIL.Modules.ContextEngine.Infrastructure;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Infrastructure;
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
builder.Services.AddExecutionModule(builder.Configuration);

var app = builder.Build();

app.MapPost("/execute-intelligence", async (ExecuteIntelligenceRequest request, IExecutionService execution) =>
{
    var result = await execution.ExecuteAsync(new AIL.Modules.Execution.Application.ExecutionRequest(
        TenantId: request.TenantId,
        CapabilityKey: request.CapabilityKey,
        PromptKey: request.PromptKey,
        Variables: request.Variables ?? new Dictionary<string, string>(),
        ContextReferenceIds: request.ContextReferenceIds ?? new List<string>()));

    if (!result.IsAllowed)
    {
        // Forbid() triggers auth middleware; use explicit 403 to avoid requiring authentication services.
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Ok(new ExecuteIntelligenceResponse(
        OutputText: result.OutputText,
        ProviderKey: result.ProviderKey,
        ModelKey: result.ModelKey,
        PromptVersion: result.PromptVersion,
        AuditRecordId: result.AuditRecordId));
});

app.Run();

public partial class Program { }
