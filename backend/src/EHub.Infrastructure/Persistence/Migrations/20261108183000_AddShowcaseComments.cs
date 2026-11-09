using System;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261108183000_AddShowcaseComments")]
public partial class AddShowcaseComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "showcase_comments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                is_team_reply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_showcase_comments", x => x.id);
                table.ForeignKey("FK_showcase_comments_showcase_comments_parent_comment_id", x => x.parent_comment_id, "showcase_comments", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_showcase_comments_showcase_projects_showcase_project_id", x => x.showcase_project_id, "showcase_projects", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_showcase_comments_users_author_user_id", x => x.author_user_id, "users", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_showcase_comments_author_user_id", table: "showcase_comments", column: "author_user_id");
        migrationBuilder.CreateIndex(name: "IX_showcase_comments_parent_comment_id", table: "showcase_comments", column: "parent_comment_id");
        migrationBuilder.CreateIndex(name: "IX_showcase_comments_showcase_project_id_created_at", table: "showcase_comments", columns: new[] { "showcase_project_id", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "showcase_comments");
}
