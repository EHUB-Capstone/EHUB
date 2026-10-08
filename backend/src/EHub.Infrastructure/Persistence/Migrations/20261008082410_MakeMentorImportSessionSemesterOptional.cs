using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeMentorImportSessionSemesterOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "mentor_import_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Master-list sessions have no semester and are short-lived (30 minutes), so they can be dropped on rollback.
            migrationBuilder.Sql("DELETE FROM mentor_import_sessions WHERE semester_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "mentor_import_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
