using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassManagementBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_major_locked",
                table: "classes",
                newName: "is_enrollment_major_locked");

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at_utc",
                table: "classes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archived_by_user_id",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "primary_lecturer_id",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_classes_archived_by_user_id",
                table: "classes",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_primary_lecturer_id",
                table: "classes",
                column: "primary_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_semester_id_course_id_primary_lecturer_id_status",
                table: "classes",
                columns: new[] { "semester_id", "course_id", "primary_lecturer_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_classes_users_archived_by_user_id",
                table: "classes",
                column: "archived_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_classes_users_primary_lecturer_id",
                table: "classes",
                column: "primary_lecturer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_users_archived_by_user_id",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "FK_classes_users_primary_lecturer_id",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_archived_by_user_id",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_primary_lecturer_id",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_semester_id_course_id_primary_lecturer_id_status",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "archived_by_user_id",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "primary_lecturer_id",
                table: "classes");

            migrationBuilder.RenameColumn(
                name: "is_enrollment_major_locked",
                table: "classes",
                newName: "is_major_locked");
        }
    }
}
