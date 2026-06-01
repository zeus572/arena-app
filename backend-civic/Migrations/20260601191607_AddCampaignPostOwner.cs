using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignPostOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "CampaignPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "CampaignPosts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPosts_OwnerUserId_CandidateId_CreatedAt",
                table: "CampaignPosts",
                columns: new[] { "OwnerUserId", "CandidateId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampaignPosts_OwnerUserId_CandidateId_CreatedAt",
                table: "CampaignPosts");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "CampaignPosts");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "CampaignPosts");
        }
    }
}
