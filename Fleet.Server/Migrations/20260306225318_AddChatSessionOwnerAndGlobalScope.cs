using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionOwnerAndGlobalScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProjectId",
                table: "ChatSessions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "ChatSessions" AS cs
                SET "OwnerId" = p."OwnerId"
                FROM "Projects" AS p
                WHERE cs."ProjectId" = p."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_OwnerId_ProjectId",
                table: "ChatSessions",
                columns: new[] { "OwnerId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_OwnerId_ProjectId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ChatSessions");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectId",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
