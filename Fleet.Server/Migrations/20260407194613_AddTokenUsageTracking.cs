using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CachedInputTokens",
                table: "MonthlyUsageLedgers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "InputTokens",
                table: "MonthlyUsageLedgers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "OutputTokens",
                table: "MonthlyUsageLedgers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "AgentPhaseResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "AgentPhaseResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedInputTokens",
                table: "MonthlyUsageLedgers");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "MonthlyUsageLedgers");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "MonthlyUsageLedgers");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "AgentPhaseResults");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "AgentPhaseResults");
        }
    }
}
