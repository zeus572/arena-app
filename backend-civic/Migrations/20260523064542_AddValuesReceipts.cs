using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddValuesReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValuesReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnswerCountAtTime = table.Column<int>(type: "integer", nullable: false),
                    ProfileVersionAtTime = table.Column<int>(type: "integer", nullable: false),
                    LearnedInsights = table.Column<string>(type: "jsonb", nullable: false),
                    ChangedAxes = table.Column<string>(type: "jsonb", nullable: false),
                    UncertainAreas = table.Column<string>(type: "jsonb", nullable: false),
                    Tensions = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuesReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValuesReceipts_UserId",
                table: "ValuesReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuesReceipts_UserId_CreatedAt",
                table: "ValuesReceipts",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValuesReceipts");
        }
    }
}
