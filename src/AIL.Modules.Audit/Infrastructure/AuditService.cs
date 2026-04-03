using AIL.Modules.Audit.Application;
using AIL.Modules.Audit.Domain;

namespace AIL.Modules.Audit.Infrastructure;

internal sealed class AuditService : IAuditService
{
    public Task<Guid> RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        // Placeholder audit sink: generates a new ID and does not persist.
        return Task.FromResult(Guid.NewGuid());
    }
}
