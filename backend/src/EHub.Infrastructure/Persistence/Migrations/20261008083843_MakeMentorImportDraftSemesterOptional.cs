using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeMentorImportDraftSemesterOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "mentor_import_drafts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Master-list drafts have no semester to fall back to, so rolling back removes them (re-import the workbook to restore).
            migrationBuilder.Sql("DELETE FROM mentor_import_drafts WHERE semester_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "semester_id",
                table: "mentor_import_drafts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
