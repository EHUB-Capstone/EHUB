using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseAutomaticReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                table: "showcase_reports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "content_fingerprint",
                table: "showcase_reports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detection_key",
                table: "showcase_reports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "showcase_reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.CreateTable(
                name: "showcase_moderation_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phrase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_moderation_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "showcase_moderation_scans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_moderation_scans", x => x.id);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_scans_showcase_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "showcase_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_scans_showcase_projects_showcase_projec~",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000001"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "đồ ngu", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000002"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "óc chó", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000003"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "fuck you", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000004"), "Possible threatening content / Có dấu hiệu đe dọa", true, "kill yourself", "Harassment" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_comment_id_detection_key",
                table: "showcase_reports",
                columns: new[] { "comment_id", "detection_key" },
                unique: true,
                filter: "source = 'System' AND comment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_showcase_project_id_detection_key",
                table: "showcase_reports",
                columns: new[] { "showcase_project_id", "detection_key" },
                unique: true,
                filter: "source = 'System' AND comment_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_showcase_reports_source",
                table: "showcase_reports",
                sql: "(source = 'User' AND reporter_user_id IS NOT NULL AND detection_key IS NULL AND content_fingerprint IS NULL) OR (source = 'System' AND reporter_user_id IS NULL AND detection_key IS NOT NULL AND content_fingerprint IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_scans_comment_id",
                table: "showcase_moderation_scans",
                column: "comment_id",
                unique: true,
                filter: "comment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_scans_showcase_project_id",
                table: "showcase_moderation_scans",
                column: "showcase_project_id",
                unique: true,
                filter: "comment_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // System detections have no reporter; keep all human reports and restore the old foreign key safely.
            migrationBuilder.Sql("DELETE FROM showcase_reports WHERE source = 'System';");
            migrationBuilder.DropTable(
                name: "showcase_moderation_rules");

            migrationBuilder.DropTable(
                name: "showcase_moderation_scans");

            migrationBuilder.DropIndex(
                name: "IX_showcase_reports_comment_id_detection_key",
                table: "showcase_reports");

            migrationBuilder.DropIndex(
                name: "IX_showcase_reports_showcase_project_id_detection_key",
                table: "showcase_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_showcase_reports_source",
                table: "showcase_reports");

            migrationBuilder.DropColumn(
                name: "content_fingerprint",
                table: "showcase_reports");

            migrationBuilder.DropColumn(
                name: "detection_key",
                table: "showcase_reports");

            migrationBuilder.DropColumn(
                name: "source",
                table: "showcase_reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                table: "showcase_reports",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
