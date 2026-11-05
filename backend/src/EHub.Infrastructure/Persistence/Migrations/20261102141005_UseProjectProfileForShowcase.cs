using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseProjectProfileForShowcase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Project name is now limited to 100 characters and Description to 300. Longer existing values are cut
            // to fit before the columns are narrowed; the removed text cannot be restored by Down.
            migrationBuilder.Sql("UPDATE projects SET name = LEFT(name, 100) WHERE char_length(name) > 100;");
            migrationBuilder.Sql("UPDATE projects SET description = LEFT(description, 300) WHERE char_length(description) > 300;");

            // The showcase no longer keeps its own copy of the Project Profile text; it reads it from the project.
            migrationBuilder.DropColumn(
                name: "problem",
                table: "showcase_projects");

            migrationBuilder.DropColumn(
                name: "solution",
                table: "showcase_projects");

            migrationBuilder.DropColumn(
                name: "tagline",
                table: "showcase_projects");

            migrationBuilder.DropColumn(
                name: "target_customers",
                table: "showcase_projects");

            migrationBuilder.DropColumn(
                name: "title",
                table: "showcase_projects");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "projects",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "problem",
                table: "showcase_projects",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "solution",
                table: "showcase_projects",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tagline",
                table: "showcase_projects",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_customers",
                table: "showcase_projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "showcase_projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
