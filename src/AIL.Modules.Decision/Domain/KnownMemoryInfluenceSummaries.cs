namespace AIL.Modules.Decision.Domain;

/// <summary>
/// Bounded vocabulary for operator-visible memory influence (no memory content, keys, or scores).
/// </summary>
public static class KnownMemoryInfluenceSummaries
{
    public const string NoMemory = "no_memory";
    public const string MemoryEmpty = "memory_empty";
    public const string MemoryNeutral = "memory_neutral";
    public const string MemoryReinforced = "memory_reinforced";
    public const string MemoryConsistent = "memory_consistent";
    public const string MemoryConflict = "memory_conflict";
}
