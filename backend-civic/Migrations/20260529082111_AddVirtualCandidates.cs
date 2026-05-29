using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateFollows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateFollows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateMutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateMutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectionCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ElectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrimarySeasonStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneralSeasonStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostReactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    FragmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Office = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    District = table.Column<int>(type: "integer", nullable: true),
                    Party = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsIncumbent = table.Column<bool>(type: "boolean", nullable: false),
                    Bio = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Background = table.Column<string>(type: "text", nullable: false),
                    ArchetypeKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DefaultTone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultIntensity = table.Column<int>(type: "integer", nullable: false),
                    AvatarBaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Intensity = table.Column<int>(type: "integer", nullable: false),
                    IssueTags = table.Column<string[]>(type: "text[]", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggerBriefingSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TriggerPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CitedReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpCount = table.Column<int>(type: "integer", nullable: false),
                    DownCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPosts_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateAxisScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AxisKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateAxisScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateAxisScores_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateIssueTones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Issue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Intensity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateIssueTones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateIssueTones_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    IssueTags = table.Column<string[]>(type: "text[]", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSources_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformPlanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IssueTags = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPlanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformPlanks_VirtualCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "VirtualCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostFragments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Start = table.Column<int>(type: "integer", nullable: false),
                    End = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    UpCount = table.Column<int>(type: "integer", nullable: false),
                    DownCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostFragments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostFragments_CampaignPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CampaignPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPosts_CandidateId_CreatedAt",
                table: "CampaignPosts",
                columns: new[] { "CandidateId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPosts_CreatedAt",
                table: "CampaignPosts",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPosts_TriggerBriefingSlug",
                table: "CampaignPosts",
                column: "TriggerBriefingSlug");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAxisScores_CandidateId_AxisKey",
                table: "CandidateAxisScores",
                columns: new[] { "CandidateId", "AxisKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateFollows_UserId_CandidateId",
                table: "CandidateFollows",
                columns: new[] { "UserId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateIssueTones_CandidateId_Issue",
                table: "CandidateIssueTones",
                columns: new[] { "CandidateId", "Issue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateMutes_UserId_CandidateId",
                table: "CandidateMutes",
                columns: new[] { "UserId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSources_CandidateId_Priority",
                table: "CandidateSources",
                columns: new[] { "CandidateId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionCycles_IsCurrent",
                table: "ElectionCycles",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionCycles_Slug",
                table: "ElectionCycles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformPlanks_CandidateId",
                table: "PlatformPlanks",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_PostFragments_PostId_Order",
                table: "PostFragments",
                columns: new[] { "PostId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_FragmentId",
                table: "PostReactions",
                column: "FragmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_PostId",
                table: "PostReactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_UserId_PostId",
                table: "PostReactions",
                columns: new[] { "UserId", "PostId" },
                unique: true,
                filter: "\"FragmentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_UserId_PostId_FragmentId",
                table: "PostReactions",
                columns: new[] { "UserId", "PostId", "FragmentId" },
                unique: true,
                filter: "\"FragmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCandidates_Office_State_District",
                table: "VirtualCandidates",
                columns: new[] { "Office", "State", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCandidates_Slug",
                table: "VirtualCandidates",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateAxisScores");

            migrationBuilder.DropTable(
                name: "CandidateFollows");

            migrationBuilder.DropTable(
                name: "CandidateIssueTones");

            migrationBuilder.DropTable(
                name: "CandidateMutes");

            migrationBuilder.DropTable(
                name: "CandidateSources");

            migrationBuilder.DropTable(
                name: "ElectionCycles");

            migrationBuilder.DropTable(
                name: "PlatformPlanks");

            migrationBuilder.DropTable(
                name: "PostFragments");

            migrationBuilder.DropTable(
                name: "PostReactions");

            migrationBuilder.DropTable(
                name: "CampaignPosts");

            migrationBuilder.DropTable(
                name: "VirtualCandidates");
        }
    }
}
