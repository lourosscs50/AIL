using Microsoft.EntityFrameworkCore;

namespace AIL.Modules.Decision.Infrastructure.Persistence;

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
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
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
