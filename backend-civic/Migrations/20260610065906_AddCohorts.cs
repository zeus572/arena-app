using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCohorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cohorts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WeekStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnchorLeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetSize = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cohorts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CohortMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CohortId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsAgent = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortMembers_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CohortMembers_CohortId",
                table: "CohortMembers",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMembers_UserId_WeekKey",
                table: "CohortMembers",
                columns: new[] { "UserId", "WeekKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cohorts_AnchorLeagueId_WeekKey",
                table: "Cohorts",
                columns: new[] { "AnchorLeagueId", "WeekKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CohortMembers");

            migrationBuilder.DropTable(
                name: "Cohorts");
        }
    }
}
