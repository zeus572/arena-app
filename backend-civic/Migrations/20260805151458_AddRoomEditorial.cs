using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomEditorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublishGateResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomRevision = table.Column<int>(type: "integer", nullable: false),
                    Gate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Blocking = table.Column<bool>(type: "boolean", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ClearedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishGateResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublishGateResults_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggerObjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TriggerObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewFlags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishGateResults_RoomId_Gate_RoomRevision",
                table: "PublishGateResults",
                columns: new[] { "RoomId", "Gate", "RoomRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewFlags_ObjectType_ObjectId_ResolvedAt",
                table: "ReviewFlags",
                columns: new[] { "ObjectType", "ObjectId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewFlags_Open_Unique",
                table: "ReviewFlags",
                columns: new[] { "ObjectType", "ObjectId", "Reason", "TriggerObjectId" },
                unique: true,
                filter: "\"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewFlags_ResolvedAt_CreatedAt",
                table: "ReviewFlags",
                columns: new[] { "ResolvedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublishGateResults");

            migrationBuilder.DropTable(
                name: "ReviewFlags");
        }
    }
}
