using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemPrObservedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastObservedPullRequestState",
                table: "WorkItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastObservedPullRequestUrl",
                table: "WorkItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastObservedPullRequestState",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "LastObservedPullRequestUrl",
                table: "WorkItems");
        }
    }
}
