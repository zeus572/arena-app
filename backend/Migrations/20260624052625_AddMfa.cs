using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaEnrolledAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecretEnc",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MfaBackupCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaBackupCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaBackupCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaBackupCodes_CodeHash",
                table: "MfaBackupCodes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_MfaBackupCodes_UserId",
                table: "MfaBackupCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_TokenHash",
                table: "TrustedDevices",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_UserId",
                table: "TrustedDevices",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaBackupCodes");

            migrationBuilder.DropTable(
                name: "TrustedDevices");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaEnrolledAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpSecretEnc",
                table: "Users");
        }
    }
}
