using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseUpvotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "showcase_upvotes",
                columns: table => new
                {
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_upvotes", x => new { x.showcase_project_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_showcase_upvotes_showcase_projects_showcase_project_id",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_showcase_upvotes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_showcase_upvotes_user_id",
                table: "showcase_upvotes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the contribution from votes that will be dropped and keep any pre-existing counts.
            migrationBuilder.Sql("""
                UPDATE showcase_projects AS showcase
                SET like_count = showcase.like_count - votes.total
                FROM (
                    SELECT showcase_project_id, COUNT(*)::integer AS total
                    FROM showcase_upvotes
                    GROUP BY showcase_project_id
                ) AS votes
                WHERE showcase.id = votes.showcase_project_id;
                """);

            migrationBuilder.DropTable(
                name: "showcase_upvotes");
        }
    }
}
