using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitProjectFundedAchievement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_funded",
                table: "projects",
                newName: "is_internally_funded");

            migrationBuilder.AddColumn<bool>(
                name: "is_externally_funded",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_externally_funded",
                table: "projects");

            migrationBuilder.RenameColumn(
                name: "is_internally_funded",
                table: "projects",
                newName: "is_funded");
        }
    }
}
