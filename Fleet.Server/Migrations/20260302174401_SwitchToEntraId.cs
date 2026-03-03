using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToEntraId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Username",
                table: "UserProfiles");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "UserProfiles",
                newName: "EntraObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_EntraObjectId",
                table: "UserProfiles",
                column: "EntraObjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_EntraObjectId",
                table: "UserProfiles");

            migrationBuilder.RenameColumn(
                name: "EntraObjectId",
                table: "UserProfiles",
                newName: "PasswordHash");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Username",
                table: "UserProfiles",
                column: "Username",
                unique: true);
        }
    }
}
