using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoalitionActs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoalitionActs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Payload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GovernanceScore = table.Column<int>(type: "integer", nullable: false),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoalitionActs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionActs_ProvisionId_Type",
                table: "CoalitionActs",
                columns: new[] { "ProvisionId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionActs_UserId_CreatedAt",
                table: "CoalitionActs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoalitionActs");
        }
    }
}
