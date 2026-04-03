using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsMetadataToDebates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewsHeadline",
                table: "GeneratedTopics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NewsPublishedAt",
                table: "GeneratedTopics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewsSource",
                table: "GeneratedTopics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedTopicId",
                table: "Debates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debates_GeneratedTopicId",
                table: "Debates",
                column: "GeneratedTopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Debates_GeneratedTopics_GeneratedTopicId",
                table: "Debates",
                column: "GeneratedTopicId",
                principalTable: "GeneratedTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debates_GeneratedTopics_GeneratedTopicId",
                table: "Debates");

            migrationBuilder.DropIndex(
                name: "IX_Debates_GeneratedTopicId",
                table: "Debates");

            migrationBuilder.DropColumn(
                name: "NewsHeadline",
                table: "GeneratedTopics");

            migrationBuilder.DropColumn(
                name: "NewsPublishedAt",
                table: "GeneratedTopics");

            migrationBuilder.DropColumn(
                name: "NewsSource",
                table: "GeneratedTopics");

            migrationBuilder.DropColumn(
                name: "GeneratedTopicId",
                table: "Debates");
        }
    }
}
