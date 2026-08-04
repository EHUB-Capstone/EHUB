using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations;

[Migration("20260804000000_AddSubjectCheckpointRubricConfiguration")]
public partial class AddSubjectCheckpointRubricConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "requirements_json", table: "checkpoints", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>(name: "key", table: "rubric_criteria", type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "levels_json", table: "rubric_criteria", type: "text", nullable: false, defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "requirements_json", table: "checkpoints");
        migrationBuilder.DropColumn(name: "key", table: "rubric_criteria");
        migrationBuilder.DropColumn(name: "levels_json", table: "rubric_criteria");
    }
}
