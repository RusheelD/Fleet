using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentExecutionSubFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentExecutions_ProjectId",
                table: "AgentExecutions");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionMode",
                table: "AgentExecutions",
                type: "text",
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<string>(
                name: "ParentExecutionId",
                table: "AgentExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_ParentExecutionId",
                table: "AgentExecutions",
                column: "ParentExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_ProjectId_ParentExecutionId",
                table: "AgentExecutions",
                columns: new[] { "ProjectId", "ParentExecutionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentExecutions_AgentExecutions_ParentExecutionId",
                table: "AgentExecutions",
                column: "ParentExecutionId",
                principalTable: "AgentExecutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentExecutions_AgentExecutions_ParentExecutionId",
                table: "AgentExecutions");

            migrationBuilder.DropIndex(
                name: "IX_AgentExecutions_ParentExecutionId",
                table: "AgentExecutions");

            migrationBuilder.DropIndex(
                name: "IX_AgentExecutions_ProjectId_ParentExecutionId",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "AgentExecutions");

            migrationBuilder.DropColumn(
                name: "ParentExecutionId",
                table: "AgentExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_ProjectId",
                table: "AgentExecutions",
                column: "ProjectId");
        }
    }
}
