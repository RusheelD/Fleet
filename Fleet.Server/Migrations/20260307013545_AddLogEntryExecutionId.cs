using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLogEntryExecutionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionId",
                table: "LogEntries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_ProjectId_ExecutionId",
                table: "LogEntries",
                columns: new[] { "ProjectId", "ExecutionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogEntries_ProjectId_ExecutionId",
                table: "LogEntries");

            migrationBuilder.DropColumn(
                name: "ExecutionId",
                table: "LogEntries");
        }
    }
}
