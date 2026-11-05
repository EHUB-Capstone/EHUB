using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "showcase_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_lineage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tagline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    problem = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    target_customers = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    solution = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    highlights = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    development_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    show_team_members = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    like_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    comment_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_showcase_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "showcase_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_showcase_links_showcase_projects_showcase_project_id",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "showcase_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_showcase_media_showcase_projects_showcase_project_id",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "showcase_review_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_review_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_showcase_review_histories_showcase_projects_showcase_projec~",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_showcase_review_histories_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_showcase_links_showcase_project_id",
                table: "showcase_links",
                column: "showcase_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_media_showcase_project_id",
                table: "showcase_media",
                column: "showcase_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_projects_project_lineage_id",
                table: "showcase_projects",
                column: "project_lineage_id",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_projects_published_at",
                table: "showcase_projects",
                column: "published_at");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_projects_status",
                table: "showcase_projects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_review_histories_actor_id",
                table: "showcase_review_histories",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_review_histories_showcase_project_id_created_at",
                table: "showcase_review_histories",
                columns: new[] { "showcase_project_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "showcase_links");

            migrationBuilder.DropTable(
                name: "showcase_media");

            migrationBuilder.DropTable(
                name: "showcase_review_histories");

            migrationBuilder.DropTable(
                name: "showcase_projects");
        }
    }
}
