using System;

namespace AIL.Modules.Decision.Infrastructure.Persistence;

/// <summary>
/// SQLite row for operator-safe decision history only (no prompts, memory bodies, or raw model data).
/// </summary>
internal sealed class DecisionHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CorrelationGroupId { get; set; }
    public Guid? ExecutionInstanceId { get; set; }
    public string DecisionType { get; set; } = "";
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string SelectedStrategyKey { get; set; } = "";
    public string? SelectedOptionId { get; set; }
    public string ConfidenceTier { get; set; } = "";
    public string PolicyKey { get; set; } = "";
    public string ReasonSummary { get; set; } = "";
    /// <summary>JSON array of strings.</summary>
    public string ConsideredStrategiesJson { get; set; } = "[]";
    public bool UsedMemory { get; set; }
    public int MemoryItemCount { get; set; }
    public string MemoryInfluenceSummary { get; set; } = "";
    /// <summary>JSON array of option snapshots (bounded fields only).</summary>
    public string OptionsJson { get; set; } = "[]";
    public string Outcome { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
