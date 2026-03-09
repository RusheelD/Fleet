using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryGitHubAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LinkedAccounts_UserProfileId",
                table: "LinkedAccounts");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "LinkedAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                WITH ranked_accounts AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY "UserProfileId"
                            ORDER BY "ConnectedAt" DESC NULLS LAST, "Id" DESC
                        ) AS rn
                    FROM "LinkedAccounts"
                    WHERE "Provider" = 'GitHub'
                )
                UPDATE "LinkedAccounts" AS account
                SET "IsPrimary" = (ranked_accounts.rn = 1)
                FROM ranked_accounts
                WHERE account."Id" = ranked_accounts."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedAccounts_UserProfileId_Provider",
                table: "LinkedAccounts",
                columns: new[] { "UserProfileId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedAccounts_UserProfileId_Provider_IsPrimary",
                table: "LinkedAccounts",
                columns: new[] { "UserProfileId", "Provider", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true AND \"Provider\" = 'GitHub'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LinkedAccounts_UserProfileId_Provider",
                table: "LinkedAccounts");

            migrationBuilder.DropIndex(
                name: "IX_LinkedAccounts_UserProfileId_Provider_IsPrimary",
                table: "LinkedAccounts");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "LinkedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedAccounts_UserProfileId",
                table: "LinkedAccounts",
                column: "UserProfileId");
        }
    }
}
