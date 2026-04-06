using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIL.Modules.Decision.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI (<c>dotnet ef migrations</c>). Not used at runtime.
/// </summary>
internal sealed class DecisionHistoryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DecisionHistoryDbContext>
{
    public DecisionHistoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DecisionHistoryDbContext>();
        optionsBuilder.UseSqlite("Data Source=ail_decision_history_design.db");
        return new DecisionHistoryDbContext(optionsBuilder.Options);
    }
}
