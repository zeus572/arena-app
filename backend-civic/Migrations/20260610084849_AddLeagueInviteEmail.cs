using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueInviteEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "LeagueInvites",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvites_LeagueId_Email",
                table: "LeagueInvites",
                columns: new[] { "LeagueId", "Email" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeagueInvites_LeagueId_Email",
                table: "LeagueInvites");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "LeagueInvites");
        }
    }
}
