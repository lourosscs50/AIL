namespace AIL.Modules.Execution.Application.Visibility;

/// <summary>MemoryCore participation facts for operators (no raw memory payloads).</summary>
public sealed record ExecutionMemoryVisibility(
    bool MemoryRequested,
    int? MemoryItemCount,
    string? ParticipationSummary);
