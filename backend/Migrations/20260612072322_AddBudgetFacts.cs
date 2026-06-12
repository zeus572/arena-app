using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    TensionLabel = table.Column<string>(type: "text", nullable: false),
                    PerspectiveA = table.Column<string>(type: "text", nullable: false),
                    SourceA = table.Column<string>(type: "text", nullable: false),
                    SourceUrlA = table.Column<string>(type: "text", nullable: false),
                    PerspectiveB = table.Column<string>(type: "text", nullable: false),
                    SourceB = table.Column<string>(type: "text", nullable: false),
                    SourceUrlB = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetFacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetFacts_FactDate_IsActive",
                table: "BudgetFacts",
                columns: new[] { "FactDate", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetFacts");
        }
    }
}
