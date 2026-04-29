using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionDynamicIteration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DynamicIterationBranch",
                table: "ChatSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DynamicIterationPolicyJson",
                table: "ChatSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDynamicIterationEnabled",
                table: "ChatSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DynamicIterationBranch",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "DynamicIterationPolicyJson",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "IsDynamicIterationEnabled",
                table: "ChatSessions");
        }
    }
}
