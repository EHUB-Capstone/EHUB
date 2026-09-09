using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyStartupIndustryTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "startup_sub_industries");

            migrationBuilder.DropColumn(
                name: "description",
                table: "startup_industries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "startup_industries",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "startup_sub_industries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    startup_industry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                name: "IX_startup_sub_industries_startup_industry_id_display_order",
                table: "startup_sub_industries",
                columns: new[] { "startup_industry_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_startup_sub_industries_startup_industry_id_normalized_name",
                table: "startup_sub_industries",
                columns: new[] { "startup_industry_id", "normalized_name" },
                unique: true);
        }
    }
}
