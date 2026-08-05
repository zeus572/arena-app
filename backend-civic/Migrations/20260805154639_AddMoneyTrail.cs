using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoneyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Jurisdiction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SourceProgramName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AmountUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountMinUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountMaxUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DollarBasis = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RealBaseYear = table.Column<int>(type: "integer", nullable: true),
                    FiscalYearStart = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearEnd = table.Column<int>(type: "integer", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    IsNet = table.Column<bool>(type: "boolean", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    WhatThisDoesNotMean = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidesNext = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EstimateMethod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Exclusions = table.Column<string[]>(type: "text[]", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Breakdown = table.Column<string>(type: "jsonb", nullable: true),
                    Comparisons = table.Column<string>(type: "jsonb", nullable: true),
                    Provenance = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoneyItems_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MoneyStageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MoneyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AmountUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Applicability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NotApplicableReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnactedByPolicyRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyStageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoneyStageEntries_MoneyItems_MoneyItemId",
                        column: x => x.MoneyItemId,
                        principalTable: "MoneyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyItems_RoomId_CurrentStage",
                table: "MoneyItems",
                columns: new[] { "RoomId", "CurrentStage" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyItems_Slug",
                table: "MoneyItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyStageEntries_MoneyItemId_Stage",
                table: "MoneyStageEntries",
                columns: new[] { "MoneyItemId", "Stage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoneyStageEntries");

            migrationBuilder.DropTable(
                name: "MoneyItems");
        }
    }
}
