using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIL.Modules.Decision.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenDecisionHistoryQueryIndexes : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// SQLite-specific DDL: uses IF EXISTS / IF NOT EXISTS so this migration stays honest when a file was
        /// previously materialized with <c>EnsureCreated</c> against a newer model (indexes may already match)
        /// or when the dropped single-column index was never created.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DecisionHistory_TenantId";""");

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_DecisionHistory_TenantId_DecisionType_CreatedAtUtc"
                ON "DecisionHistory" ("TenantId", "DecisionType", "CreatedAtUtc");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_DecisionHistory_TenantId_CorrelationGroupId_CreatedAtUtc"
                ON "DecisionHistory" ("TenantId", "CorrelationGroupId", "CreatedAtUtc");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_DecisionHistory_TenantId_ExecutionInstanceId_CreatedAtUtc"
                ON "DecisionHistory" ("TenantId", "ExecutionInstanceId", "CreatedAtUtc");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DecisionHistory_TenantId_DecisionType_CreatedAtUtc";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DecisionHistory_TenantId_CorrelationGroupId_CreatedAtUtc";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DecisionHistory_TenantId_ExecutionInstanceId_CreatedAtUtc";""");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionHistory_TenantId",
                table: "DecisionHistory",
                column: "TenantId");
        }
    }
}
