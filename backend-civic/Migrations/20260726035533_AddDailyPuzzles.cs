using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPuzzles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPuzzles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PuzzleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Edition = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    Locality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SourceBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceProvisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNewsItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPuzzles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyPuzzlePlays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PuzzleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    AttemptsUsed = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPuzzlePlays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPuzzlePlays_DailyPuzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "DailyPuzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzlePlays_PuzzleId_UserId",
                table: "DailyPuzzlePlays",
                columns: new[] { "PuzzleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzlePlays_UserId_CreatedAt",
                table: "DailyPuzzlePlays",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzles_Kind_PuzzleDate_Locality",
                table: "DailyPuzzles",
                columns: new[] { "Kind", "PuzzleDate", "Locality" },
                unique: true,
                filter: "\"Locality\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzles_Kind_PuzzleDate_National",
                table: "DailyPuzzles",
                columns: new[] { "Kind", "PuzzleDate" },
                unique: true,
                filter: "\"Locality\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzles_Kind_Status_PuzzleDate",
                table: "DailyPuzzles",
                columns: new[] { "Kind", "Status", "PuzzleDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPuzzlePlays");

            migrationBuilder.DropTable(
                name: "DailyPuzzles");
        }
    }
}
