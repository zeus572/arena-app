using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCompromiseMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Turns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Turns");
        }
    }
}
