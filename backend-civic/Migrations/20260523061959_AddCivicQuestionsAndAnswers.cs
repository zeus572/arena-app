using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCivicQuestionsAndAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CivicQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Choices = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CivicAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedChoiceKey = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Intensity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasoningChoice = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FreeTextReasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CivicAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CivicAnswers_CivicQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "CivicQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CivicAnswers_QuestionId",
                table: "CivicAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicAnswers_UserId",
                table: "CivicAnswers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CivicAnswers_UserId_QuestionId",
                table: "CivicAnswers",
                columns: new[] { "UserId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CivicQuestions_ExternalId",
                table: "CivicQuestions",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CivicQuestions_Type_Order",
                table: "CivicQuestions",
                columns: new[] { "Type", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CivicAnswers");

            migrationBuilder.DropTable(
                name: "CivicQuestions");
        }
    }
}
