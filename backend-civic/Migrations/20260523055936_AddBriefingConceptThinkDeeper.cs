using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBriefingConceptThinkDeeper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Briefings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Headline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Institution = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Branch = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AudienceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyConcept = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Summary30 = table.Column<string>(type: "text", nullable: false),
                    Summary3Min = table.Column<string>(type: "text", nullable: false),
                    Summary10Min = table.Column<string>(type: "text", nullable: false),
                    WhoActed = table.Column<string>(type: "text", nullable: false),
                    WhatChanged = table.Column<string>(type: "text", nullable: false),
                    WhyItMatters = table.Column<string>(type: "text", nullable: false),
                    Disagreement = table.Column<string>(type: "text", nullable: false),
                    StrongestArgumentFor = table.Column<string>(type: "text", nullable: false),
                    StrongestArgumentAgainst = table.Column<string>(type: "text", nullable: false),
                    ValuesInConflict = table.Column<string[]>(type: "text[]", nullable: false),
                    ThinkDeeperQuestion = table.Column<string>(type: "text", nullable: false),
                    RelatedConcepts = table.Column<string[]>(type: "text[]", nullable: false),
                    WhereToGoNext = table.Column<string[]>(type: "text[]", nullable: false),
                    IssueOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Briefings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlainDefinition = table.Column<string>(type: "text", nullable: false),
                    WhyItMatters = table.Column<string>(type: "text", nullable: false),
                    WhereYouSeeIt = table.Column<string[]>(type: "text[]", nullable: false),
                    CurrentExample = table.Column<string>(type: "text", nullable: false),
                    CommonMisunderstanding = table.Column<string>(type: "text", nullable: false),
                    RelatedConcepts = table.Column<string[]>(type: "text[]", nullable: false),
                    TryItQuestion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThinkDeepers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Issue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstReactionPrompt = table.Column<string>(type: "text", nullable: false),
                    Values = table.Column<string[]>(type: "text[]", nullable: false),
                    StrongestArgumentA = table.Column<string>(type: "text", nullable: false),
                    StrongestArgumentB = table.Column<string>(type: "text", nullable: false),
                    WhatSideAMayMiss = table.Column<string>(type: "text", nullable: false),
                    WhatSideBMayMiss = table.Column<string>(type: "text", nullable: false),
                    WhatWouldChangeYourMind = table.Column<string[]>(type: "text[]", nullable: false),
                    CanBothBeTrue = table.Column<string>(type: "text", nullable: false),
                    BuildYourViewPrompt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThinkDeepers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BriefingWordsToKnow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Term = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Definition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BriefingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BriefingWordsToKnow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BriefingWordsToKnow_Briefings_BriefingId",
                        column: x => x.BriefingId,
                        principalTable: "Briefings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BriefingWordsToKnow_BriefingId",
                table: "BriefingWordsToKnow",
                column: "BriefingId");

            migrationBuilder.CreateIndex(
                name: "IX_Briefings_IssueOrder",
                table: "Briefings",
                column: "IssueOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Briefings_Slug",
                table: "Briefings",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_Slug",
                table: "Concepts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThinkDeepers_Slug",
                table: "ThinkDeepers",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BriefingWordsToKnow");

            migrationBuilder.DropTable(
                name: "Concepts");

            migrationBuilder.DropTable(
                name: "ThinkDeepers");

            migrationBuilder.DropTable(
                name: "Briefings");
        }
    }
}
