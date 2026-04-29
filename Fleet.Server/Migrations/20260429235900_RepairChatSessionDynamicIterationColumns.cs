using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Server.Migrations
{
    /// <inheritdoc />
    public partial class RepairChatSessionDynamicIterationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "DynamicIterationBranch" text;
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "DynamicIterationPolicyJson" text;
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "IsDynamicIterationEnabled" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "BranchStrategy" text NOT NULL DEFAULT 'AutoFromProjectPattern';
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "SessionPinnedBranch" text;
                ALTER TABLE "ChatSessions" ADD COLUMN IF NOT EXISTS "InheritParentBranchForSubFlows" boolean NOT NULL DEFAULT TRUE;

                UPDATE "ChatSessions"
                SET
                    "IsDynamicIterationEnabled" = COALESCE("IsDynamicIterationEnabled", FALSE),
                    "BranchStrategy" = COALESCE(NULLIF("BranchStrategy", ''), 'AutoFromProjectPattern'),
                    "InheritParentBranchForSubFlows" = COALESCE("InheritParentBranchForSubFlows", TRUE);

                ALTER TABLE "ChatSessions" ALTER COLUMN "IsDynamicIterationEnabled" SET DEFAULT FALSE;
                ALTER TABLE "ChatSessions" ALTER COLUMN "IsDynamicIterationEnabled" SET NOT NULL;
                ALTER TABLE "ChatSessions" ALTER COLUMN "BranchStrategy" SET DEFAULT 'AutoFromProjectPattern';
                ALTER TABLE "ChatSessions" ALTER COLUMN "BranchStrategy" SET NOT NULL;
                ALTER TABLE "ChatSessions" ALTER COLUMN "InheritParentBranchForSubFlows" SET DEFAULT TRUE;
                ALTER TABLE "ChatSessions" ALTER COLUMN "InheritParentBranchForSubFlows" SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "InheritParentBranchForSubFlows";
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "SessionPinnedBranch";
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "BranchStrategy";
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "IsDynamicIterationEnabled";
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "DynamicIterationPolicyJson";
                ALTER TABLE "ChatSessions" DROP COLUMN IF EXISTS "DynamicIterationBranch";
                """);
        }
    }
}
