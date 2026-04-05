using AIL.Modules.MemoryCore.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

public sealed class MemoryContextAssembler : IMemoryContextAssembler
{
    public Task<MemoryContext> AssembleAsync(RetrieveMemoryResponse response)
    {
        var items = response.Records.Select(r => new AIL.Modules.MemoryCore.Contracts.MemoryContextItem(
            Key: r.Key,
            Content: r.Content,
            Importance: r.Importance,
            Source: r.Source,
            ScopeType: r.ScopeType,
            ScopeId: r.ScopeId,
            CreatedAtUtc: r.CreatedAtUtc,
            UpdatedAtUtc: r.UpdatedAtUtc)).ToList();

        return Task.FromResult(new MemoryContext(items));
    }
}