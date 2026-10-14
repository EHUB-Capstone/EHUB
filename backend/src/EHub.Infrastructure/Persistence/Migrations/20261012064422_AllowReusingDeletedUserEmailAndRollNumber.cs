using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowReusingDeletedUserEmailAndRollNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_normalized_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_students_normalized_roll_number",
                table: "students");

            migrationBuilder.DropIndex(
                name: "IX_students_roll_number",
                table: "students");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_students_normalized_roll_number",
                table: "students",
                column: "normalized_roll_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_students_roll_number",
                table: "students",
                column: "roll_number",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_normalized_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_students_normalized_roll_number",
                table: "students");

            migrationBuilder.DropIndex(
                name: "IX_students_roll_number",
                table: "students");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_students_normalized_roll_number",
                table: "students",
                column: "normalized_roll_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_students_roll_number",
                table: "students",
                column: "roll_number",
                unique: true);
        }
    }
}
