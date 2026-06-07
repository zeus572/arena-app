using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoalitionLeaguesAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoalitionActivityDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoalitionActivityDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoalitionLeagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GapTier = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoalitionLeagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoalitionLeagueMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SpectrumBucket = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AgeBand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoalitionLeagueMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoalitionLeagueMembers_CoalitionLeagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "CoalitionLeagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionActivityDays_UserId_Day",
                table: "CoalitionActivityDays",
                columns: new[] { "UserId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionLeagueMembers_LeagueId",
                table: "CoalitionLeagueMembers",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionLeagueMembers_UserId",
                table: "CoalitionLeagueMembers",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoalitionActivityDays");

            migrationBuilder.DropTable(
                name: "CoalitionLeagueMembers");

            migrationBuilder.DropTable(
                name: "CoalitionLeagues");
        }
    }
}
