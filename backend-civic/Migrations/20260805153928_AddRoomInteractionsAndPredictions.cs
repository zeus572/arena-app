using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomInteractionsAndPredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LearningObjective = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ScoringMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sensitivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AgeGuidance = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AnswerDependsOnClaimStatus = table.Column<bool>(type: "boolean", nullable: false),
                    ContentRevision = table.Column<int>(type: "integer", nullable: false),
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interactions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Proposition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResolutionCriteria = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResolutionSourceDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResolutionSourceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationPolicy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OpensAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvesByAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EditorialOwner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ForecastCount = table.Column<int>(type: "integer", nullable: false),
                    MeanProbability = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RoomInteractionPlays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomInteractionPlays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomInteractionPlays_Interactions_InteractionId",
                        column: x => x.InteractionId,
                        principalTable: "Interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    UpdateCount = table.Column<int>(type: "integer", nullable: false),
                    BrierScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPredictions_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_RoomId_Ordinal",
                table: "Interactions",
                columns: new[] { "RoomId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_Slug",
                table: "Interactions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Outcome_ResolvesByAt",
                table: "Predictions",
                columns: new[] { "Outcome", "ResolvesByAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_RoomId",
                table: "Predictions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Slug",
                table: "Predictions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomInteractionPlays_InteractionId_UserId_Phase",
                table: "RoomInteractionPlays",
                columns: new[] { "InteractionId", "UserId", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPredictions_PredictionId_UserId",
                table: "UserPredictions",
                columns: new[] { "PredictionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPredictions_UserId_CreatedAt",
                table: "UserPredictions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomInteractionPlays");

            migrationBuilder.DropTable(
                name: "UserPredictions");

            migrationBuilder.DropTable(
                name: "Interactions");

            migrationBuilder.DropTable(
                name: "Predictions");
        }
    }
}
