using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class CampaignManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Debates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CampaignWeek",
                table: "Debates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateName = table.Column<string>(type: "text", nullable: false),
                    PersonaId = table.Column<string>(type: "text", nullable: false),
                    Persona = table.Column<string>(type: "text", nullable: false),
                    OpponentName = table.Column<string>(type: "text", nullable: false),
                    OpponentPersona = table.Column<string>(type: "text", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    PlatformJson = table.Column<string>(type: "text", nullable: false),
                    CurrentWeek = table.Column<int>(type: "integer", nullable: false),
                    TotalWeeks = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Approval = table.Column<double>(type: "double precision", nullable: false),
                    Won = table.Column<bool>(type: "boolean", nullable: true),
                    FinalApproval = table.Column<double>(type: "double precision", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    LastResolvedDebateWeek = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    EventKey = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    ResponseChosen = table.Column<string>(type: "text", nullable: true),
                    OutcomeJson = table.Column<string>(type: "text", nullable: true),
                    Resolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignEvents_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Budget = table.Column<double>(type: "double precision", nullable: false),
                    TimeUnits = table.Column<int>(type: "integer", nullable: false),
                    StaffCount = table.Column<int>(type: "integer", nullable: false),
                    Momentum = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignResources_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRating = table.Column<double>(type: "double precision", nullable: false),
                    DecisionsJson = table.Column<string>(type: "text", nullable: false),
                    ResourceChangesJson = table.Column<string>(type: "text", nullable: false),
                    DebateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignWeeks_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Debates_CampaignId",
                table: "Debates",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignEvents_CampaignId_WeekNumber",
                table: "CampaignEvents",
                columns: new[] { "CampaignId", "WeekNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignResources_CampaignId",
                table: "CampaignResources",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignWeeks_CampaignId_WeekNumber",
                table: "CampaignWeeks",
                columns: new[] { "CampaignId", "WeekNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_UserId",
                table: "Campaigns",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignEvents");

            migrationBuilder.DropTable(
                name: "CampaignResources");

            migrationBuilder.DropTable(
                name: "CampaignWeeks");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Debates_CampaignId",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "CampaignWeek",
                table: "Debates");
        }
    }
}
