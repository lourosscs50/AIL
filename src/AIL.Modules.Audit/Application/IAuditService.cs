using AIL.Modules.Audit.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIL.Modules.Audit.Application;

public interface IAuditService
{
    Task<Guid> RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
