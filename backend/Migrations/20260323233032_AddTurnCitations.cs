using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitationsJson",
                table: "Turns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CitationsJson",
                table: "Turns");
        }
    }
}
