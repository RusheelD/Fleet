using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionBranchStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchStrategy",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                defaultValue: "AutoFromProjectPattern");

            migrationBuilder.AddColumn<string>(
                name: "SessionPinnedBranch",
                table: "ChatSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InheritParentBranchForSubFlows",
                table: "ChatSessions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchStrategy",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "SessionPinnedBranch",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "InheritParentBranchForSubFlows",
                table: "ChatSessions");
        }
    }
}
