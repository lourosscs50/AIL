using Microsoft.EntityFrameworkCore;

namespace AIL.Modules.Decision.Infrastructure.Persistence;

/// <summary>
/// EF Core context for operator-safe decision history rows. Schema lifecycle is managed with compiled migrations under <c>Migrations/</c>; at runtime <see cref="DecisionHistoryDatabaseInitializer"/> applies pending migrations (host startup and first store use when hosted services do not run).
/// </summary>
internal sealed class DecisionHistoryDbContext : DbContext
{
    public DecisionHistoryDbContext(DbContextOptions<DecisionHistoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<DecisionHistoryEntity> DecisionHistory => Set<DecisionHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DecisionHistoryEntity>(e =>
        {
            e.ToTable("DecisionHistory");
            e.HasKey(x => x.Id);
            // List queries are always tenant-scoped and ordered by CreatedAtUtc (then Id). Single-column TenantId alone is redundant with composites below.
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
            // Optional exact-match filters in EfDecisionHistoryStore.List — composites support tenant + filter + chronological access (sort/range on CreatedAtUtc).
            e.HasIndex(x => new { x.TenantId, x.DecisionType, x.CreatedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.CorrelationGroupId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.ExecutionInstanceId, x.CreatedAtUtc });
            e.Property(x => x.ReasonSummary).HasMaxLength(4096);
            e.Property(x => x.DecisionType).HasMaxLength(512);
            e.Property(x => x.SubjectType).HasMaxLength(256);
            e.Property(x => x.SubjectId).HasMaxLength(256);
            e.Property(x => x.SelectedStrategyKey).HasMaxLength(512);
            e.Property(x => x.SelectedOptionId).HasMaxLength(512);
            e.Property(x => x.ConfidenceTier).HasMaxLength(64);
            e.Property(x => x.PolicyKey).HasMaxLength(512);
            e.Property(x => x.MemoryInfluenceSummary).HasMaxLength(128);
            e.Property(x => x.Outcome).HasMaxLength(64);
            e.Property(x => x.ConsideredStrategiesJson);
            e.Property(x => x.OptionsJson);
        });
    }
}
