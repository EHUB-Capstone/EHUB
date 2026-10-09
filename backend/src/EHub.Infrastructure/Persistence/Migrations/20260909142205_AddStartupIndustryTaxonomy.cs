using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStartupIndustryTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "startup_industries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_startup_industries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "startup_sub_industries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    startup_industry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_startup_sub_industries", x => x.id);
                    table.ForeignKey(
                        name: "FK_startup_sub_industries_startup_industries_startup_industry_~",
                        column: x => x.startup_industry_id,
                        principalTable: "startup_industries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_startup_industries_normalized_name",
                table: "startup_industries",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_startup_sub_industries_startup_industry_id_display_order",
                table: "startup_sub_industries",
                columns: new[] { "startup_industry_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_startup_sub_industries_startup_industry_id_normalized_name",
                table: "startup_sub_industries",
                columns: new[] { "startup_industry_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "startup_sub_industries");

            migrationBuilder.DropTable(
                name: "startup_industries");
        }
    }
}
