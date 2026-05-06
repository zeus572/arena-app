using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddArenasAndForks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArenaId",
                table: "Debates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForkNote",
                table: "Debates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ForkedFromDebateId",
                table: "Debates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Arenas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    Rules = table.Column<string>(type: "text", nullable: false),
                    DefaultFormat = table.Column<string>(type: "text", nullable: false),
                    IconEmoji = table.Column<string>(type: "text", nullable: false),
                    AccentColor = table.Column<string>(type: "text", nullable: false),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arenas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Debates_ArenaId",
                table: "Debates",
                column: "ArenaId");

            migrationBuilder.CreateIndex(
                name: "IX_Debates_ForkedFromDebateId",
                table: "Debates",
                column: "ForkedFromDebateId");

            migrationBuilder.CreateIndex(
                name: "IX_Arenas_Slug",
                table: "Arenas",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Debates_Arenas_ArenaId",
                table: "Debates",
                column: "ArenaId",
                principalTable: "Arenas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Debates_Debates_ForkedFromDebateId",
                table: "Debates",
                column: "ForkedFromDebateId",
                principalTable: "Debates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debates_Arenas_ArenaId",
                table: "Debates");

            migrationBuilder.DropForeignKey(
                name: "FK_Debates_Debates_ForkedFromDebateId",
                table: "Debates");

            migrationBuilder.DropTable(
                name: "Arenas");

            migrationBuilder.DropIndex(
                name: "IX_Debates_ArenaId",
                table: "Debates");

            migrationBuilder.DropIndex(
                name: "IX_Debates_ForkedFromDebateId",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "ArenaId",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "ForkNote",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "ForkedFromDebateId",
                table: "Debates");
        }
    }
}
