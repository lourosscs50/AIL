using AIL.Modules.MemoryCore.Contracts;
using System.Threading.Tasks;

namespace AIL.Modules.Execution.Application;

public interface IMemoryContextAssembler
{
    Task<MemoryContext> AssembleAsync(RetrieveMemoryResponse response);
}