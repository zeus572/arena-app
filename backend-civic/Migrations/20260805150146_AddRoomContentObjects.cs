using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomContentObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfusionDiscriminator",
                table: "Concepts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfusionPairSlug",
                table: "Concepts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KnowledgeKind",
                table: "Concepts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortGloss",
                table: "Concepts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Actors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AlternateNames = table.Column<string[]>(type: "text[]", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActualPower = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConstrainedBy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StatedWants = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatedWantsSourceRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatedWantsAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Developments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Headline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WhyItMatters = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StoryRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GenerationSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Developments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Developments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OccurredPrecision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Marker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WhatWasKnownThen = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TextAlternative = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimelineEvents_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActorRoomRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LeverageStatement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RoleHere = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActorRoomRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActorRoomRoles_Actors_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActorRoomRoles_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActorRoomRoles_ActorId",
                table: "ActorRoomRoles",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorRoomRoles_RoomId_Tier_Ordinal",
                table: "ActorRoomRoles",
                columns: new[] { "RoomId", "Tier", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_ActorRoomRoles_Room_Actor_Decision",
                table: "ActorRoomRoles",
                columns: new[] { "RoomId", "ActorId", "DecisionKey" },
                unique: true,
                filter: "\"DecisionKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActorRoomRoles_Room_Actor_Default",
                table: "ActorRoomRoles",
                columns: new[] { "RoomId", "ActorId" },
                unique: true,
                filter: "\"DecisionKey\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Actors_ActorType",
                table: "Actors",
                column: "ActorType");

            migrationBuilder.CreateIndex(
                name: "IX_Actors_Slug",
                table: "Actors",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Developments_RoomId_OccurredAt",
                table: "Developments",
                columns: new[] { "RoomId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineEvents_RoomId_OccurredOn",
                table: "TimelineEvents",
                columns: new[] { "RoomId", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActorRoomRoles");

            migrationBuilder.DropTable(
                name: "Developments");

            migrationBuilder.DropTable(
                name: "TimelineEvents");

            migrationBuilder.DropTable(
                name: "Actors");

            migrationBuilder.DropColumn(
                name: "ConfusionDiscriminator",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "ConfusionPairSlug",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "KnowledgeKind",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "ShortGloss",
                table: "Concepts");
        }
    }
}
