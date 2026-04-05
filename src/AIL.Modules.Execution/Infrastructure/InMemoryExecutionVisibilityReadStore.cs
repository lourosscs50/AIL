using System;
using System.Collections.Generic;
using System.Linq;
using AIL.Modules.Execution.Application;
using AIL.Modules.Execution.Application.Visibility;

namespace AIL.Modules.Execution.Infrastructure;

/// <summary>
/// Process-local, bounded store for operator GET until a durable read model exists.
/// </summary>
internal sealed class InMemoryExecutionVisibilityReadStore : IExecutionVisibilityReadStore
{
    private const int MaxEntries = 512;
    private const int MaxPageSize = 100;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, ExecutionVisibilityReadModel> _byId = new();
    private readonly Queue<Guid> _insertionOrder = new();

    public void Put(ExecutionVisibilityReadModel model)
    {
        var id = model.Trace.ExecutionInstanceId;
        lock (_lock)
        {
            var isNew = !_byId.ContainsKey(id);
            if (isNew)
            {
                while (_byId.Count >= MaxEntries && _insertionOrder.Count > 0)
                {
                    var evict = _insertionOrder.Dequeue();
                    _byId.Remove(evict);
                }

                _insertionOrder.Enqueue(id);
            }

            _byId[id] = model;
        }
    }

    public ExecutionVisibilityReadModel? TryGet(Guid executionInstanceId)
    {
        lock (_lock)
        {
            return _byId.TryGetValue(executionInstanceId, out var m) ? m : null;
        }
    }

    public (IReadOnlyList<ExecutionVisibilityReadModel> Items, int TotalCount) ListByCompletedAtDescending(
        int page,
        int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var ps = pageSize < 1 ? 50 : Math.Min(pageSize, MaxPageSize);

        lock (_lock)
        {
            var ordered = _byId.Values
                .OrderByDescending(m => m.CompletedAtUtc)
                .ThenBy(m => m.Trace.ExecutionInstanceId)
                .ToList();

            var total = ordered.Count;
            var skip = (p - 1) * ps;
            List<ExecutionVisibilityReadModel> slice;
            if (skip >= ordered.Count)
                slice = new List<ExecutionVisibilityReadModel>();
            else
                slice = ordered.Skip(skip).Take(ps).ToList();

            return (slice, total);
        }
    }
}
