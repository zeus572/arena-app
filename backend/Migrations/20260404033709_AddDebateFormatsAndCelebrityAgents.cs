using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDebateFormatsAndCelebrityAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "Debates",
                type: "text",
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<string>(
                name: "AgentType",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Era",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    ExcerptText = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    ThemeTag = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSources_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebateParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebateParticipants_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebateParticipants_Debates_DebateId",
                        column: x => x.DebateId,
                        principalTable: "Debates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSources_AgentId",
                table: "AgentSources",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateParticipants_AgentId",
                table: "DebateParticipants",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateParticipants_DebateId",
                table: "DebateParticipants",
                column: "DebateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSources");

            migrationBuilder.DropTable(
                name: "DebateParticipants");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "AgentType",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Era",
                table: "Agents");
        }
    }
}
