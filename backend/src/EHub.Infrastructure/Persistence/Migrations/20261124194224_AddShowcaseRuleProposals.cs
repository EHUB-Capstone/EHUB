using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseRuleProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "showcase_moderation_rule_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    showcase_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phrase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phrase_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_moderation_rule_proposals", x => x.id);
                    table.CheckConstraint("ck_showcase_rule_proposals_category", "category IN ('Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam')");
                    table.CheckConstraint("ck_showcase_rule_proposals_status", "status IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_showcase_comments_commen~",
                        column: x => x.comment_id,
                        principalTable: "showcase_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_showcase_moderation_rule~",
                        column: x => x.created_rule_id,
                        principalTable: "showcase_moderation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_showcase_projects_showca~",
                        column: x => x.showcase_project_id,
                        principalTable: "showcase_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_showcase_reports_source_~",
                        column: x => x.source_report_id,
                        principalTable: "showcase_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_users_proposed_by_user_id",
                        column: x => x.proposed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_showcase_moderation_rule_proposals_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_comment_id",
                table: "showcase_moderation_rule_proposals",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_created_rule_id",
                table: "showcase_moderation_rule_proposals",
                column: "created_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_phrase_key",
                table: "showcase_moderation_rule_proposals",
                column: "phrase_key",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_proposed_by_user_id",
                table: "showcase_moderation_rule_proposals",
                column: "proposed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_reviewed_by_user_id",
                table: "showcase_moderation_rule_proposals",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_showcase_project_id",
                table: "showcase_moderation_rule_proposals",
                column: "showcase_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_source_report_id",
                table: "showcase_moderation_rule_proposals",
                column: "source_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_status_proposed_at_utc",
                table: "showcase_moderation_rule_proposals",
                columns: new[] { "status", "proposed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "showcase_moderation_rule_proposals");
        }
    }
}
