using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoalitionParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoalitionParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SpectrumBucket = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsAgent = table.Column<bool>(type: "boolean", nullable: false),
                    RegionJson = table.Column<string>(type: "jsonb", nullable: true),
                    IntensitiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoalitionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoalitionParticipants_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoalitionParticipants_ProvisionId_UserId",
                table: "CoalitionParticipants",
                columns: new[] { "ProvisionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoalitionParticipants");
        }
    }
}
