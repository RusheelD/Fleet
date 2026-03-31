using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessTokenExpiresAtUtc",
                table: "LinkedAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "LinkedAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAtUtc",
                table: "LinkedAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenExpiresAtUtc",
                table: "LinkedAccounts");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "LinkedAccounts");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAtUtc",
                table: "LinkedAccounts");
        }
    }
}
