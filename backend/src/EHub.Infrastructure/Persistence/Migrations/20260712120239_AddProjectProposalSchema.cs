using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProposalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    startup_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tagline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    problem = table.Column<string>(type: "text", nullable: true),
                    solution = table.Column<string>(type: "text", nullable: true),
                    target_customers = table.Column<string>(type: "text", nullable: true),
                    value_proposition = table.Column<string>(type: "text", nullable: true),
                    market_size = table.Column<string>(type: "text", nullable: true),
                    competitors = table.Column<string>(type: "text", nullable: true),
                    business_model = table.Column<string>(type: "text", nullable: true),
                    revenue_model = table.Column<string>(type: "text", nullable: true),
                    marketing_strategy = table.Column<string>(type: "text", nullable: true),
                    technology = table.Column<string>(type: "text", nullable: true),
                    financial_plan = table.Column<string>(type: "text", nullable: true),
                    roadmap = table.Column<string>(type: "text", nullable: true),
                    team_introduction = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_project_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_proposals_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposals_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposals_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposals_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposals_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_shortcuts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    shortcut_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_project_shortcuts", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_shortcuts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_shortcuts_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_shortcuts_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "startup_lineages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    startup_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_startup_lineages", x => x.id);
                    table.ForeignKey(
                        name: "FK_startup_lineages_projects_current_project_id",
                        column: x => x.current_project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_startup_lineages_projects_original_project_id",
                        column: x => x.original_project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_startup_lineages_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pitch_decks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    file_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cloudinary_public_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_pitch_decks", x => x.id);
                    table.ForeignKey(
                        name: "FK_pitch_decks_project_proposals_project_proposal_id",
                        column: x => x.project_proposal_id,
                        principalTable: "project_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pitch_decks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pitch_decks_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    section_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    selected_text = table.Column<string>(type: "text", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    thread_root_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    resolved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_project_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_comments_project_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "project_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_comments_project_comments_thread_root_id",
                        column: x => x.thread_root_id,
                        principalTable: "project_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_comments_project_proposals_project_proposal_id",
                        column: x => x.project_proposal_id,
                        principalTable: "project_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_comments_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_comments_users_resolved_by_id",
                        column: x => x.resolved_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_proposal_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    change_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_proposal_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_proposal_versions_project_proposals_project_proposa~",
                        column: x => x.project_proposal_id,
                        principalTable: "project_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_proposal_versions_users_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_cloudinary_public_id",
                table: "pitch_decks",
                column: "cloudinary_public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_project_id",
                table: "pitch_decks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_project_id_version_number",
                table: "pitch_decks",
                columns: new[] { "project_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_project_proposal_id",
                table: "pitch_decks",
                column: "project_proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_status",
                table: "pitch_decks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_uploaded_at",
                table: "pitch_decks",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "IX_pitch_decks_uploaded_by_id",
                table: "pitch_decks",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_created_by_id",
                table: "project_comments",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_parent_comment_id",
                table: "project_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_project_proposal_id",
                table: "project_comments",
                column: "project_proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_project_proposal_id_resolved",
                table: "project_comments",
                columns: new[] { "project_proposal_id", "resolved" });

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_project_proposal_id_section_key",
                table: "project_comments",
                columns: new[] { "project_proposal_id", "section_key" });

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_resolved",
                table: "project_comments",
                column: "resolved");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_resolved_by_id",
                table: "project_comments",
                column: "resolved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_section_key",
                table: "project_comments",
                column: "section_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_comments_thread_root_id",
                table: "project_comments",
                column: "thread_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_versions_changed_by_id",
                table: "project_proposal_versions",
                column: "changed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_versions_created_at",
                table: "project_proposal_versions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposal_versions_project_proposal_id_version_number",
                table: "project_proposal_versions",
                columns: new[] { "project_proposal_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_class_id",
                table: "project_proposals",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_class_id_status",
                table: "project_proposals",
                columns: new[] { "class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_created_by_id",
                table: "project_proposals",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_project_id",
                table: "project_proposals",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_status",
                table: "project_proposals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_submitted_at",
                table: "project_proposals",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_team_id",
                table: "project_proposals",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_team_id_status",
                table: "project_proposals",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_proposals_updated_by_id",
                table: "project_proposals",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_shortcuts_created_by_id",
                table: "project_shortcuts",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_shortcuts_project_id",
                table: "project_shortcuts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_shortcuts_project_id_name",
                table: "project_shortcuts",
                columns: new[] { "project_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_project_shortcuts_shortcut_type",
                table: "project_shortcuts",
                column: "shortcut_type");

            migrationBuilder.CreateIndex(
                name: "IX_project_shortcuts_team_id",
                table: "project_shortcuts",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_startup_lineages_created_by_id",
                table: "startup_lineages",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_startup_lineages_current_project_id",
                table: "startup_lineages",
                column: "current_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_startup_lineages_original_project_id",
                table: "startup_lineages",
                column: "original_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_startup_lineages_startup_name",
                table: "startup_lineages",
                column: "startup_name");

            migrationBuilder.CreateIndex(
                name: "IX_startup_lineages_status",
                table: "startup_lineages",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pitch_decks");

            migrationBuilder.DropTable(
                name: "project_comments");

            migrationBuilder.DropTable(
                name: "project_proposal_versions");

            migrationBuilder.DropTable(
                name: "project_shortcuts");

            migrationBuilder.DropTable(
                name: "startup_lineages");

            migrationBuilder.DropTable(
                name: "project_proposals");
        }
    }
}
