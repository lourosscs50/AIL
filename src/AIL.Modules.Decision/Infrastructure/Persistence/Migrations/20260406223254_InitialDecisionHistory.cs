using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIL.Modules.Decision.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDecisionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExecutionInstanceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecisionType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SubjectType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SubjectId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SelectedStrategyKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SelectedOptionId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ConfidenceTier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PolicyKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ReasonSummary = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    ConsideredStrategiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UsedMemory = table.Column<bool>(type: "INTEGER", nullable: false),
                    MemoryItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryInfluenceSummary = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionHistory_TenantId",
                table: "DecisionHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionHistory_TenantId_CreatedAtUtc",
                table: "DecisionHistory",
                columns: new[] { "TenantId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionHistory");
        }
    }
}
