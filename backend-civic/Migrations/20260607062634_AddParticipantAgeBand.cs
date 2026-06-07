using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantAgeBand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgeBand",
                table: "CoalitionParticipants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeBand",
                table: "CoalitionParticipants");
        }
    }
}
