using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class MvpGapSecurityWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "WorkItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AssignedAgentCount",
                table: "WorkItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "WorkItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BranchPattern",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommitAuthorEmail",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitAuthorMode",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommitAuthorName",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserProfileId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    ExecutionId = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_ProjectId",
                table: "NotificationEvents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_UserProfileId_IsRead_CreatedAtUtc",
                table: "NotificationEvents",
                columns: new[] { "UserProfileId", "IsRead", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationEvents");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignedAgentCount",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "BranchPattern",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CommitAuthorEmail",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CommitAuthorMode",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CommitAuthorName",
                table: "Projects");
        }
    }
}
