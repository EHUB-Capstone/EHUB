using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAchievementFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_awarded",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_funded",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "projects",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_awarded",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_funded",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "projects");
        }
    }
}
