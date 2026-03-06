using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTierUsageLedgerAndPrTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PullRequestTitle",
                table: "AgentExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MonthlyUsageLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserProfileId = table.Column<int>(type: "integer", nullable: false),
                    UtcMonth = table.Column<string>(type: "text", nullable: false),
                    WorkItemRunCharges = table.Column<int>(type: "integer", nullable: false),
                    WorkItemRunRefunds = table.Column<int>(type: "integer", nullable: false),
                    CodingRunCharges = table.Column<int>(type: "integer", nullable: false),
                    CodingRunRefunds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyUsageLedgers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyUsageLedgers_UserProfileId_UtcMonth",
                table: "MonthlyUsageLedgers",
                columns: new[] { "UserProfileId", "UtcMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyUsageLedgers");

            migrationBuilder.DropColumn(
                name: "PullRequestTitle",
                table: "AgentExecutions");
        }
    }
}
