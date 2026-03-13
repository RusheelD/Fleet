using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AttachChatAttachmentsToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatSessionId",
                table: "ChatAttachments");

            migrationBuilder.AddColumn<string>(
                name: "ChatMessageId",
                table: "ChatAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatSessionId_ChatMessageId",
                table: "ChatAttachments",
                columns: new[] { "ChatSessionId", "ChatMessageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatAttachments_ChatMessages_ChatMessageId",
                table: "ChatAttachments",
                column: "ChatMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatAttachments_ChatMessages_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ChatAttachments_ChatSessionId_ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.DropColumn(
                name: "ChatMessageId",
                table: "ChatAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAttachments_ChatSessionId",
                table: "ChatAttachments",
                column: "ChatSessionId");
        }
    }
}
