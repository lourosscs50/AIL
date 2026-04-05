using System;
using System.Collections.Generic;
using AIL.Modules.Execution.Application.Visibility;

namespace AIL.Modules.Execution.Application;

/// <summary>
/// Optional process-local store for last execution visibility snapshots (operator GET).
/// Not a source of truth across replicas until a durable projection exists.
/// </summary>
public interface IExecutionVisibilityReadStore
{
    void Put(ExecutionVisibilityReadModel model);

    ExecutionVisibilityReadModel? TryGet(Guid executionInstanceId);

    /// <summary>
    /// Read-only page of snapshots ordered by <see cref="ExecutionVisibilityReadModel.CompletedAtUtc"/> descending (newest first).
    /// <paramref name="page"/> is 1-based; <paramref name="pageSize"/> is clamped by the implementation.
    /// </summary>
    (IReadOnlyList<ExecutionVisibilityReadModel> Items, int TotalCount) ListByCompletedAtDescending(int page, int pageSize);
}
