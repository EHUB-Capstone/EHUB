using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "classes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH slug_sources AS (
                    SELECT
                        c.id,
                        LEFT(
                            COALESCE(
                                NULLIF(
                                    TRIM(BOTH '-' FROM REGEXP_REPLACE(
                                        REGEXP_REPLACE(
                                            LOWER(COALESCE(s.code, '') || '-' || COALESCE(co.code, '') || '-' || c.class_index::text),
                                            '[^a-z0-9]+',
                                            '-',
                                            'g'),
                                        '-{2,}',
                                        '-',
                                        'g')),
                                    ''),
                                'class-' || c.id::text),
                            160) AS base_slug,
                        c.created_at
                    FROM classes c
                    INNER JOIN semesters s ON s.id = c.semester_id
                    INNER JOIN courses co ON co.id = c.course_id
                ),
                ranked_slugs AS (
                    SELECT
                        id,
                        base_slug,
                        ROW_NUMBER() OVER (PARTITION BY base_slug ORDER BY created_at, id) AS slug_rank
                    FROM slug_sources
                )
                UPDATE classes c
                SET slug = CASE
                    WHEN ranked_slugs.slug_rank = 1 THEN ranked_slugs.base_slug
                    ELSE LEFT(
                        ranked_slugs.base_slug,
                        GREATEST(1, 160 - LENGTH('-' || ranked_slugs.slug_rank::text)))
                        || '-' || ranked_slugs.slug_rank::text
                    END
                FROM ranked_slugs
                WHERE c.id = ranked_slugs.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "classes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_classes_slug",
                table: "classes",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_classes_slug",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "classes");
        }
    }
}
