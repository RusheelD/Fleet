using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ProjectId",
                table: "WorkItems");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "WorkItemNumber",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill WorkItemNumber from existing data (per-project sequential)
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ProjectId" ORDER BY "Id") AS rn
                    FROM "WorkItems"
                )
                UPDATE "WorkItems" w
                SET "WorkItemNumber" = n.rn
                FROM numbered n
                WHERE w."Id" = n."Id";
                """);

            // Restart identity sequence past existing max Id
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"WorkItems"', 'Id'),
                              COALESCE((SELECT MAX("Id") FROM "WorkItems"), 0) + 1, false);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_WorkItemNumber",
                table: "WorkItems",
                columns: new[] { "ProjectId", "WorkItemNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ProjectId_WorkItemNumber",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "WorkItemNumber",
                table: "WorkItems");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId",
                table: "WorkItems",
                column: "ProjectId");
        }
    }
}
