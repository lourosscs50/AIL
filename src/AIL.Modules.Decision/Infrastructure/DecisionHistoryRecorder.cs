using System;
using AIL.Modules.Decision.Application;
using Microsoft.Extensions.Logging;

namespace AIL.Modules.Decision.Infrastructure;

internal sealed class DecisionHistoryRecorder : IDecisionHistoryRecorder
{
    private readonly IDecisionHistoryStore _store;
    private readonly ILogger<DecisionHistoryRecorder> _logger;

    public DecisionHistoryRecorder(IDecisionHistoryStore store, ILogger<DecisionHistoryRecorder> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Guid? TryRecord(DecisionRequest request, DecisionResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            var id = Guid.NewGuid();
            var record = DecisionHistoryRecordBuilder.Build(id, request, result, DateTime.UtcNow);
            _store.Put(record);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision history persistence failed for tenant {TenantId}, decision type {DecisionType}.", request.TenantId, request.DecisionType);
            return null;
        }
    }
}
