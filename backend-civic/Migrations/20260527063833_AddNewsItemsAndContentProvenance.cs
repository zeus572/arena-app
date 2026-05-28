using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsItemsAndContentProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GenerationSource",
                table: "ThinkDeepers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNewsItemId",
                table: "ThinkDeepers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSource",
                table: "QuizQuestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNewsItemId",
                table: "QuizQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSource",
                table: "Concepts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNewsItemId",
                table: "Concepts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSource",
                table: "Briefings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceNewsItemId",
                table: "Briefings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Headline = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Url = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_ExternalId",
                table: "NewsItems",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Status_IngestedAt",
                table: "NewsItems",
                columns: new[] { "Status", "IngestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropColumn(
                name: "GenerationSource",
                table: "ThinkDeepers");

            migrationBuilder.DropColumn(
                name: "SourceNewsItemId",
                table: "ThinkDeepers");

            migrationBuilder.DropColumn(
                name: "GenerationSource",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "SourceNewsItemId",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "GenerationSource",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "SourceNewsItemId",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "GenerationSource",
                table: "Briefings");

            migrationBuilder.DropColumn(
                name: "SourceNewsItemId",
                table: "Briefings");
        }
    }
}
