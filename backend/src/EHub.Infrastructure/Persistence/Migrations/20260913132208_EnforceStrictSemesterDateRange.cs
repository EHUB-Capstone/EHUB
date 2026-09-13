using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStrictSemesterDateRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters");

            migrationBuilder.AddCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters",
                sql: "start_date IS NULL OR end_date IS NULL OR start_date < end_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters");

            migrationBuilder.AddCheckConstraint(
                name: "CK_semesters_date_range",
                table: "semesters",
                sql: "start_date IS NULL OR end_date IS NULL OR start_date <= end_date");
        }
    }
}
