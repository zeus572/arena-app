using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Congress = table.Column<int>(type: "integer", nullable: false),
                    BillType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShortTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Sponsor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Party = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IntroducedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestActionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FullTextUrl = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Jurisdiction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JurisdictionRegion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SynthesisStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SynthesisSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SynthesizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillAxisPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    AxisKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillAxisPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillAxisPositions_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillAxisPositions_BillId_AxisKey",
                table: "BillAxisPositions",
                columns: new[] { "BillId", "AxisKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ExternalId",
                table: "Bills",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Jurisdiction_LatestActionDate",
                table: "Bills",
                columns: new[] { "Jurisdiction", "LatestActionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SynthesisStatus_IngestedAt",
                table: "Bills",
                columns: new[] { "SynthesisStatus", "IngestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillAxisPositions");

            migrationBuilder.DropTable(
                name: "Bills");
        }
    }
}
