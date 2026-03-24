using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Arena.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DebateTags",
                columns: table => new
                {
                    DebateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateTags", x => new { x.DebateId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DebateTags_Debates_DebateId",
                        column: x => x.DebateId,
                        principalTable: "Debates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebateTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebateTags_TagId",
                table: "DebateTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            // Full-text search vector (stored generated column + GIN index)
            migrationBuilder.Sql("""
                ALTER TABLE "Debates" ADD COLUMN "SearchVector" tsvector
                    GENERATED ALWAYS AS (
                        setweight(to_tsvector('english', coalesce("Topic", '')), 'A') ||
                        setweight(to_tsvector('english', coalesce("Description", '')), 'B')
                    ) STORED;
                """);
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Debates_SearchVector" ON "Debates" USING GIN ("SearchVector");
                """);

            // Performance indexes
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Debates_Status" ON "Debates" ("Status");
                """);
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Debates_UpdatedAt" ON "Debates" ("UpdatedAt" DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebateTags");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
