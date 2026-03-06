using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class UserScopedProjectSlugAndWorkItemPrLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Slug",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "LinkedPullRequestUrl",
                table: "WorkItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId_Slug",
                table: "Projects",
                columns: new[] { "OwnerId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId_Slug",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LinkedPullRequestUrl",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                table: "Projects",
                column: "Slug",
                unique: true);
        }
    }
}
