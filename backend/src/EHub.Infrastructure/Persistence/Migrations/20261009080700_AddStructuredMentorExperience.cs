using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredMentorExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mentor_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    years = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_experiences", x => x.id);
                    table.CheckConstraint("ck_mentor_experience_kind", "kind IN ('Startup', 'Technology')");
                    table.CheckConstraint("ck_mentor_experience_years", "years IS NULL OR (years >= 0 AND years <= 80)");
                    table.ForeignKey(
                        name: "FK_mentor_experiences_mentor_profiles_mentor_profile_id",
                        column: x => x.mentor_profile_id,
                        principalTable: "mentor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_experiences_mentor_profile_id_kind_area",
                table: "mentor_experiences",
                columns: new[] { "mentor_profile_id", "kind", "area" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_experiences");
        }
    }
}
