using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    OwnerUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    MaxMembers = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueInvites_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonPoints = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdentityRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueMembers_CivicCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CivicCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeagueMembers_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueMembers_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    BriefingSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Headline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpensAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponsesCloseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VotingCloseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WinnerMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    PointsAwardedJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueRounds_LeagueMembers_WinnerMemberId",
                        column: x => x.WinnerMemberId,
                        principalTable: "LeagueMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeagueRounds_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueRoundEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OptionLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PointsEarned = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueRoundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueRoundEntries_CampaignPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CampaignPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueRoundEntries_LeagueMembers_LeagueMemberId",
                        column: x => x.LeagueMemberId,
                        principalTable: "LeagueMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueRoundEntries_LeagueRounds_LeagueRoundId",
                        column: x => x.LeagueRoundId,
                        principalTable: "LeagueRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueRoundEntries_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvites_Code",
                table: "LeagueInvites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvites_LeagueId",
                table: "LeagueInvites",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_CampaignId",
                table: "LeagueMembers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_CandidateId",
                table: "LeagueMembers",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_LeagueId_Role",
                table: "LeagueMembers",
                columns: new[] { "LeagueId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_LeagueId_UserId",
                table: "LeagueMembers",
                columns: new[] { "LeagueId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMembers_UserId",
                table: "LeagueMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRoundEntries_CandidateId",
                table: "LeagueRoundEntries",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRoundEntries_LeagueMemberId",
                table: "LeagueRoundEntries",
                column: "LeagueMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRoundEntries_LeagueRoundId_LeagueMemberId",
                table: "LeagueRoundEntries",
                columns: new[] { "LeagueRoundId", "LeagueMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRoundEntries_PostId",
                table: "LeagueRoundEntries",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRounds_LeagueId_SeasonNumber_RoundNumber",
                table: "LeagueRounds",
                columns: new[] { "LeagueId", "SeasonNumber", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRounds_LeagueId_Status",
                table: "LeagueRounds",
                columns: new[] { "LeagueId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueRounds_WinnerMemberId",
                table: "LeagueRounds",
                column: "WinnerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_OwnerUserId",
                table: "Leagues",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueInvites");

            migrationBuilder.DropTable(
                name: "LeagueRoundEntries");

            migrationBuilder.DropTable(
                name: "LeagueRounds");

            migrationBuilder.DropTable(
                name: "LeagueMembers");

            migrationBuilder.DropTable(
                name: "Leagues");
        }
    }
}
