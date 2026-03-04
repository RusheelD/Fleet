using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "AgentExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "AgentExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentPhase",
                table: "AgentExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PullRequestUrl",
                table: "AgentExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "AgentExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AgentExecutions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AgentPhaseResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: false),
                    ToolCallCount = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhaseOrder = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPhaseResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentPhaseResults_AgentExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "AgentExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPhaseResults_ExecutionId_PhaseOrder",
                table: "AgentPhaseResults",
                columns: new[] { "ExecutionId", "PhaseOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPhaseResults");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "CurrentPhase",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "PullRequestUrl",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AgentExecutions");
        }
    }
}
