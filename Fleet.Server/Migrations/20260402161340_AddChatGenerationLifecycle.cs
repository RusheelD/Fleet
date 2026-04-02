using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChatGenerationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GenerationState",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                defaultValue: "idle");

            migrationBuilder.AddColumn<string>(
                name: "GenerationStatus",
                table: "ChatSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GenerationUpdatedAtUtc",
                table: "ChatSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecentActivityJson",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenerationState",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "GenerationStatus",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "GenerationUpdatedAtUtc",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "RecentActivityJson",
                table: "ChatSessions");
        }
    }
}
