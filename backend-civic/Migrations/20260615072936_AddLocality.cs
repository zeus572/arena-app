using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Civic.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLocality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalityState",
                table: "UserProfiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "Provisions",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "NewsItems",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "Briefings",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalityState",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "Provisions");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "NewsItems");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "Briefings");
        }
    }
}
