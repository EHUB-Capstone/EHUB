using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "showcase_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_showcase_reports_showcase_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "showcase_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_showcase_reports_showcase_projects_showcase_project_id",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_showcase_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_showcase_reports_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_comment_id",
                table: "showcase_reports",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_reporter_user_id_comment_id",
                table: "showcase_reports",
                columns: new[] { "reporter_user_id", "comment_id" },
                unique: true,
                filter: "comment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_reporter_user_id_showcase_project_id",
                table: "showcase_reports",
                columns: new[] { "reporter_user_id", "showcase_project_id" },
                unique: true,
                filter: "comment_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_resolved_by_user_id",
                table: "showcase_reports",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_showcase_project_id",
                table: "showcase_reports",
                column: "showcase_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_status_created_at",
                table: "showcase_reports",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "showcase_reports");
        }
    }
}
