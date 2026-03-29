using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPersonality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Aggressiveness",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Eloquence",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Empathy",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FactReliance",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Wit",
                table: "Agents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aggressiveness",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Eloquence",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Empathy",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "FactReliance",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Wit",
                table: "Agents");
        }
    }
}
