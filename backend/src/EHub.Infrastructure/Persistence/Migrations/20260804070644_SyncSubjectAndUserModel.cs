using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncSubjectAndUserModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some existing development databases applied the now-removed
            // AddSubjectCheckpointRubricConfiguration migration, which already
            // created these columns without recording this migration ID. Keep
            // this migration safe for both those databases and a clean database.
            migrationBuilder.Sql("""
                ALTER TABLE rubric_criteria
                ADD COLUMN IF NOT EXISTS key character varying(100) NOT NULL DEFAULT '';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE rubric_criteria
                ADD COLUMN IF NOT EXISTS levels_json text NOT NULL DEFAULT '[]';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE checkpoints
                ADD COLUMN IF NOT EXISTS requirements_json text NOT NULL DEFAULT '[]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "key",
                table: "rubric_criteria");

            migrationBuilder.DropColumn(
                name: "levels_json",
                table: "rubric_criteria");

            migrationBuilder.DropColumn(
                name: "requirements_json",
                table: "checkpoints");
        }
    }
}
