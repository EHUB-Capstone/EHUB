using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorProfileTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "mentor_tags",
                table: "mentor_profiles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "startup_domains",
                table: "mentor_profiles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "technology_skills",
                table: "mentor_profiles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mentor_tags",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "startup_domains",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "technology_skills",
                table: "mentor_profiles");
        }
    }
}
