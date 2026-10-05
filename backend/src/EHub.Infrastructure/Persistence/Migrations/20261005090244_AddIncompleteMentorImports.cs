using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncompleteMentorImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mentor_import_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_ordinal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    fpt_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    contract_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    education_level = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    current_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    job_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    converted_mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_mentor_import_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_mentor_import_drafts_mentor_profiles_converted_mentor_profi~",
                        column: x => x.converted_mentor_profile_id,
                        principalTable: "mentor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mentor_import_drafts_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_drafts_converted_mentor_profile_id",
                table: "mentor_import_drafts",
                column: "converted_mentor_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_drafts_semester_id_mentor_type_normalized_ful~",
                table: "mentor_import_drafts",
                columns: new[] { "semester_id", "mentor_type", "normalized_full_name" });

            migrationBuilder.CreateIndex(
                name: "IX_mentor_import_drafts_semester_id_status",
                table: "mentor_import_drafts",
                columns: new[] { "semester_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_import_drafts");
        }
    }
}
