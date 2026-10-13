using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxClassActiveScheduleConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_active_requires_lecturer_and_schedule",
                table: "classes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_active_requires_lecturer",
                table: "classes",
                sql: "status <> 'Active' OR primary_lecturer_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_active_requires_lecturer",
                table: "classes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_active_requires_lecturer_and_schedule",
                table: "classes",
                sql: "status <> 'Active' OR (primary_lecturer_id IS NOT NULL AND schedule_json IS NOT NULL AND jsonb_typeof(schedule_json) = 'array' AND jsonb_array_length(schedule_json) > 0)");
        }
    }
}
