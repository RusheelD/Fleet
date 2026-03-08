using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropLogEntriesProjectIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogEntries_ProjectId",
                table: "LogEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_ProjectId",
                table: "LogEntries",
                column: "ProjectId");
        }
    }
}
