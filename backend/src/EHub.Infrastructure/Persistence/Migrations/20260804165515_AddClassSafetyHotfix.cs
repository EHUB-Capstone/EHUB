using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSafetyHotfix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "classes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "class_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_audit_logs_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_audit_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Repair legacy assignment rows before enforcing one primary lecturer per class.
            // Some older records only populated classes.primary_lecturer_id and never created
            // the corresponding class_lecturers row.
            migrationBuilder.Sql(
                """
                INSERT INTO class_lecturers
                    (class_id, lecturer_id, is_primary, assigned_at, assigned_by_id)
                SELECT
                    c.id,
                    c.primary_lecturer_id,
                    TRUE,
                    COALESCE(c.updated_at, c.created_at, NOW()),
                    NULL
                FROM classes AS c
                WHERE c.primary_lecturer_id IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM class_lecturers AS cl
                      WHERE cl.class_id = c.id
                        AND cl.lecturer_id = c.primary_lecturer_id
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE class_lecturers AS cl
                SET is_primary = (c.primary_lecturer_id = cl.lecturer_id)
                FROM classes AS c
                WHERE c.id = cl.class_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers",
                column: "class_id",
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "IX_class_audit_logs_class_id_occurred_at_utc",
                table: "class_audit_logs",
                columns: new[] { "class_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_class_audit_logs_performed_by_user_id",
                table: "class_audit_logs",
                column: "performed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_class_lecturers_class_id",
                table: "class_lecturers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "classes");
        }
    }
}
