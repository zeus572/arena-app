using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCoalitionProvisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Provisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NeutralText = table.Column<string>(type: "text", nullable: false),
                    SourceBriefingId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceBriefingSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RelevantAxes = table.Column<string[]>(type: "text[]", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProvisionPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Stance = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Intensity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasoningTag = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisionPositions_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProvisionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtractedPositions = table.Column<string>(type: "jsonb", nullable: false),
                    IsExtracted = table.Column<bool>(type: "boolean", nullable: false),
                    ExtractionModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisionVersions_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TradeoffDescription = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PositionOptions = table.Column<string[]>(type: "text[]", nullable: false),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IntroducedByVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubQuestions_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcceptanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Accept = table.Column<bool>(type: "boolean", nullable: false),
                    Intensity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasoningTag = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcceptanceRecords_ProvisionVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ProvisionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcceptanceRecords_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Amendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FreeFormText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProposedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Amendments_ProvisionVersions_ProposedVersionId",
                        column: x => x.ProposedVersionId,
                        principalTable: "ProvisionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Amendments_Provisions_ProvisionId",
                        column: x => x.ProvisionId,
                        principalTable: "Provisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptanceRecords_ProvisionId",
                table: "AcceptanceRecords",
                column: "ProvisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AcceptanceRecords_UserId_VersionId",
                table: "AcceptanceRecords",
                columns: new[] { "UserId", "VersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcceptanceRecords_VersionId",
                table: "AcceptanceRecords",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Amendments_ProposedVersionId",
                table: "Amendments",
                column: "ProposedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Amendments_ProvisionId",
                table: "Amendments",
                column: "ProvisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionPositions_ProvisionId",
                table: "ProvisionPositions",
                column: "ProvisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionPositions_ProvisionId_UserId",
                table: "ProvisionPositions",
                columns: new[] { "ProvisionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionVersions_ProvisionId",
                table: "ProvisionVersions",
                column: "ProvisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionVersions_ProvisionId_TextHash",
                table: "ProvisionVersions",
                columns: new[] { "ProvisionId", "TextHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Provisions_Slug",
                table: "Provisions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provisions_SourceBriefingId",
                table: "Provisions",
                column: "SourceBriefingId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisions_State",
                table: "Provisions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_SubQuestions_ProvisionId_Key",
                table: "SubQuestions",
                columns: new[] { "ProvisionId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptanceRecords");

            migrationBuilder.DropTable(
                name: "Amendments");

            migrationBuilder.DropTable(
                name: "ProvisionPositions");

            migrationBuilder.DropTable(
                name: "SubQuestions");

            migrationBuilder.DropTable(
                name: "ProvisionVersions");

            migrationBuilder.DropTable(
                name: "Provisions");
        }
    }
}
