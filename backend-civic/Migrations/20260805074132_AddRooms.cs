using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoomDensity",
                table: "UserProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RoomDensityConsecutiveBoard",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Dek = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Sensitivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Locality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    LastMeaningfulUpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DraftModelId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DraftPromptVersion = table.Column<int>(type: "integer", nullable: false),
                    DraftAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DraftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    StoryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    HowItWorksIntro = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TypePayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    TypePayloadVersion = table.Column<int>(type: "integer", nullable: true),
                    SourceBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNewsItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceBriefingId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlternateTitles = table.Column<string[]>(type: "text[]", nullable: true),
                    MatchTerms = table.Column<string[]>(type: "text[]", nullable: true),
                    ScopeStatement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InclusionRules = table.Column<string[]>(type: "text[]", nullable: true),
                    ExclusionRules = table.Column<string[]>(type: "text[]", nullable: true),
                    CurrentStatusSentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TopUnresolvedQuestion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WatchNext = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MonitoringCadence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FreshnessOwner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArticlesConsideredCount = table.Column<int>(type: "integer", nullable: true),
                    DevelopmentWindowDays = table.Column<int>(type: "integer", nullable: true),
                    EssentialFacts = table.Column<string>(type: "jsonb", nullable: true),
                    NextSteps = table.Column<string>(type: "jsonb", nullable: true),
                    Provenance = table.Column<string>(type: "jsonb", nullable: true),
                    Stakeholders = table.Column<string>(type: "jsonb", nullable: true),
                    TerminologyNotes = table.Column<string>(type: "jsonb", nullable: true),
                    WhyItMatters = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    IsMeaningful = table.Column<bool>(type: "boolean", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GateApprovals = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomRevisions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoomStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenRevision = table.Column<int>(type: "integer", nullable: false),
                    LastVisitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Following = table.Column<bool>(type: "boolean", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SectionProgress = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoomStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoomStates_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsMeaningful = table.Column<bool>(type: "boolean", nullable: false),
                    Headline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    WhyItMatters = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ObjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrectionKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeLogEntries_RoomRevisions_RoomRevisionId",
                        column: x => x.RoomRevisionId,
                        principalTable: "RoomRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_RoomId_IsMeaningful_CreatedAt",
                table: "ChangeLogEntries",
                columns: new[] { "RoomId", "IsMeaningful", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_RoomId_RevisionNumber",
                table: "ChangeLogEntries",
                columns: new[] { "RoomId", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_RoomRevisionId",
                table: "ChangeLogEntries",
                column: "RoomRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomRevisions_RoomId_Revision",
                table: "RoomRevisions",
                columns: new[] { "RoomId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Slug",
                table: "Rooms",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Status_LastMeaningfulUpdateAt",
                table: "Rooms",
                columns: new[] { "Status", "LastMeaningfulUpdateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Status_Locality",
                table: "Rooms",
                columns: new[] { "Status", "Locality" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoomStates_RoomId_Following",
                table: "UserRoomStates",
                columns: new[] { "RoomId", "Following" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoomStates_UserId_RoomId",
                table: "UserRoomStates",
                columns: new[] { "UserId", "RoomId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeLogEntries");

            migrationBuilder.DropTable(
                name: "UserRoomStates");

            migrationBuilder.DropTable(
                name: "RoomRevisions");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropColumn(
                name: "RoomDensity",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "RoomDensityConsecutiveBoard",
                table: "UserProfiles");
        }
    }
}
