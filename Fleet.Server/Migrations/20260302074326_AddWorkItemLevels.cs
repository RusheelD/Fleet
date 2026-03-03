using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelId",
                table: "WorkItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IconName = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemLevels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_LevelId",
                table: "WorkItems",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLevels_ProjectId_Name",
                table: "WorkItemLevels",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItemLevels_LevelId",
                table: "WorkItems",
                column: "LevelId",
                principalTable: "WorkItemLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItemLevels_LevelId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkItemLevels");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_LevelId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "WorkItems");
        }
    }
}
