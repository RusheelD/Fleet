using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServerConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpServerConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserProfileId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TransportType = table.Column<string>(type: "text", nullable: false, defaultValue: "stdio"),
                    Command = table.Column<string>(type: "text", nullable: true),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    WorkingDirectory = table.Column<string>(type: "text", nullable: true),
                    Endpoint = table.Column<string>(type: "text", nullable: true),
                    ProtectedEnvironmentVariables = table.Column<string>(type: "text", nullable: true),
                    ProtectedHeaders = table.Column<string>(type: "text", nullable: true),
                    BuiltInTemplateKey = table.Column<string>(type: "text", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastValidationError = table.Column<string>(type: "text", nullable: true),
                    LastToolCount = table.Column<int>(type: "integer", nullable: false),
                    DiscoveredToolsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpServerConnections_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpServerConnections_UserProfileId_Enabled",
                table: "McpServerConnections",
                columns: new[] { "UserProfileId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_McpServerConnections_UserProfileId_Name",
                table: "McpServerConnections",
                columns: new[] { "UserProfileId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpServerConnections");
        }
    }
}
