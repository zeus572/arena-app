using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomsGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedTextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WhatWouldSettleIt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Predicate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ObjectValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TimeScopeStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeScopeEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeographyScope = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    StaleAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ShareImpressionCount = table.Column<int>(type: "integer", nullable: false),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ObjectLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FromId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    SourceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProposedBy = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VerifiedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetrievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Jurisdiction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RightsNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FullTextAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Availability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HasInterest = table.Column<bool>(type: "boolean", nullable: false),
                    InterestNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceNewsItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceRefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TriggerSourceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ClaimId_ChangedAt",
                table: "ClaimStatusHistories",
                columns: new[] { "ClaimId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_NormalizedTextHash",
                table: "Claims",
                column: "NormalizedTextHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Slug",
                table: "Claims",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status_LastReviewedAt",
                table: "Claims",
                columns: new[] { "Status", "LastReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectLinks_FromType_FromId_Relation",
                table: "ObjectLinks",
                columns: new[] { "FromType", "FromId", "Relation" });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectLinks_Open_Unique",
                table: "ObjectLinks",
                columns: new[] { "FromType", "FromId", "Relation", "ToType", "ToId" },
                unique: true,
                filter: "\"ValidTo\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectLinks_ToType_ToId_Relation",
                table: "ObjectLinks",
                columns: new[] { "ToType", "ToId", "Relation" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRefs_Availability",
                table: "SourceRefs",
                column: "Availability");

            migrationBuilder.CreateIndex(
                name: "IX_SourceRefs_SourceType_PublishedAt",
                table: "SourceRefs",
                columns: new[] { "SourceType", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRefs_UrlHash",
                table: "SourceRefs",
                column: "UrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimStatusHistories");

            migrationBuilder.DropTable(
                name: "ObjectLinks");

            migrationBuilder.DropTable(
                name: "SourceRefs");

            migrationBuilder.DropTable(
                name: "Claims");
        }
    }
}
