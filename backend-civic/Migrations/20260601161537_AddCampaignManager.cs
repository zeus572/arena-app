using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CivicCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RaceLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalWeeks = table.Column<int>(type: "integer", nullable: false),
                    CurrentWeek = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Won = table.Column<bool>(type: "boolean", nullable: true),
                    FinalSupport = table.Column<double>(type: "double precision", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ActionsRemaining = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicCampaigns_ElectionCycles_ElectionCycleId",
                        column: x => x.ElectionCycleId,
                        principalTable: "ElectionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CivicCampaigns_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CivicCampaignActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Target = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SupportDelta = table.Column<double>(type: "double precision", nullable: false),
                    GeneratedPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCampaignActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicCampaignActions_CivicCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CivicCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CivicCampaignStandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPlayer = table.Column<bool>(type: "boolean", nullable: false),
                    SupportShare = table.Column<double>(type: "double precision", nullable: false),
                    Momentum = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCampaignStandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicCampaignStandings_CivicCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CivicCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CivicCampaignStandings_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CivicCampaignWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    PlayerSupportAfter = table.Column<double>(type: "double precision", nullable: false),
                    SalientIssuesJson = table.Column<string>(type: "text", nullable: false),
                    StandingsJson = table.Column<string>(type: "text", nullable: false),
                    DeltaBreakdownJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicCampaignWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicCampaignWeeks_CivicCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CivicCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaignActions_CampaignId_WeekNumber",
                table: "CivicCampaignActions",
                columns: new[] { "CampaignId", "WeekNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaignStandings_CampaignId_CandidateId",
                table: "CivicCampaignStandings",
                columns: new[] { "CampaignId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaignStandings_CandidateId",
                table: "CivicCampaignStandings",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaignWeeks_CampaignId_WeekNumber",
                table: "CivicCampaignWeeks",
                columns: new[] { "CampaignId", "WeekNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaigns_CandidateId",
                table: "CivicCampaigns",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaigns_ElectionCycleId",
                table: "CivicCampaigns",
                column: "ElectionCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaigns_UserId",
                table: "CivicCampaigns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicCampaigns_UserId_Status",
                table: "CivicCampaigns",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CivicCampaignActions");

            migrationBuilder.DropTable(
                name: "CivicCampaignStandings");

            migrationBuilder.DropTable(
                name: "CivicCampaignWeeks");

            migrationBuilder.DropTable(
                name: "CivicCampaigns");
        }
    }
}
