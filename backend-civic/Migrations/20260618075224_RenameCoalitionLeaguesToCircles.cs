using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameCoalitionLeaguesToCircles : Migration
    {
        // The coalition cohort/skill-tier concept was renamed League -> Circle to free up
        // "League" for the social (friends) league. The tables are renamed in place (rather
        // than dropped/recreated) so any existing membership/cohort rows are preserved.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "CoalitionLeagues", newName: "CoalitionCircles");
            migrationBuilder.RenameTable(name: "CoalitionLeagueMembers", newName: "CoalitionCircleMembers");

            migrationBuilder.RenameColumn(name: "LeagueId", table: "CoalitionCircleMembers", newName: "CircleId");

            migrationBuilder.RenameIndex(
                name: "IX_CoalitionLeagueMembers_LeagueId",
                newName: "IX_CoalitionCircleMembers_CircleId",
                table: "CoalitionCircleMembers");
            migrationBuilder.RenameIndex(
                name: "IX_CoalitionLeagueMembers_UserId",
                newName: "IX_CoalitionCircleMembers_UserId",
                table: "CoalitionCircleMembers");

            // Postgres keeps the old constraint names through a table rename — rename them so the
            // schema matches what a fresh CreateTable would produce (keeps future migrations clean).
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircles"" RENAME CONSTRAINT ""PK_CoalitionLeagues"" TO ""PK_CoalitionCircles"";");
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircleMembers"" RENAME CONSTRAINT ""PK_CoalitionLeagueMembers"" TO ""PK_CoalitionCircleMembers"";");
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircleMembers"" RENAME CONSTRAINT ""FK_CoalitionLeagueMembers_CoalitionLeagues_LeagueId"" TO ""FK_CoalitionCircleMembers_CoalitionCircles_CircleId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircleMembers"" RENAME CONSTRAINT ""FK_CoalitionCircleMembers_CoalitionCircles_CircleId"" TO ""FK_CoalitionLeagueMembers_CoalitionLeagues_LeagueId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircleMembers"" RENAME CONSTRAINT ""PK_CoalitionCircleMembers"" TO ""PK_CoalitionLeagueMembers"";");
            migrationBuilder.Sql(@"ALTER TABLE ""CoalitionCircles"" RENAME CONSTRAINT ""PK_CoalitionCircles"" TO ""PK_CoalitionLeagues"";");

            migrationBuilder.RenameIndex(
                name: "IX_CoalitionCircleMembers_UserId",
                newName: "IX_CoalitionLeagueMembers_UserId",
                table: "CoalitionCircleMembers");
            migrationBuilder.RenameIndex(
                name: "IX_CoalitionCircleMembers_CircleId",
                newName: "IX_CoalitionLeagueMembers_LeagueId",
                table: "CoalitionCircleMembers");

            migrationBuilder.RenameColumn(name: "CircleId", table: "CoalitionCircleMembers", newName: "LeagueId");

            migrationBuilder.RenameTable(name: "CoalitionCircleMembers", newName: "CoalitionLeagueMembers");
            migrationBuilder.RenameTable(name: "CoalitionCircles", newName: "CoalitionLeagues");
        }
    }
}
