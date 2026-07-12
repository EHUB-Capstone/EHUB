using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    group_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_chat_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_groups_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_groups_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_groups_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    data_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_chat_group_members", x => x.id);
                    table.CheckConstraint("CK_ChatGroupMember_HasIdentification", "NOT (user_id IS NULL AND student_id IS NULL)");
                    table.CheckConstraint("CK_ChatGroupMember_SingleMemberType", "NOT (user_id IS NOT NULL AND student_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_chat_group_members_chat_groups_chat_group_id",
                        column: x => x.chat_group_id,
                        principalTable: "chat_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_group_members_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sender_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attachment_json = table.Column<string>(type: "jsonb", nullable: true),
                    reactions_json = table.Column<string>(type: "jsonb", nullable: true),
                    mentions_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_messages_chat_groups_chat_group_id",
                        column: x => x.chat_group_id,
                        principalTable: "chat_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id",
                table: "chat_group_members",
                column: "chat_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_student_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_user_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_is_active",
                table: "chat_group_members",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_role",
                table: "chat_group_members",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_student_id",
                table: "chat_group_members",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_user_id",
                table: "chat_group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_class_id",
                table: "chat_groups",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_class_id_group_type",
                table: "chat_groups",
                columns: new[] { "class_id", "group_type" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_created_by_id",
                table: "chat_groups",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_group_type",
                table: "chat_groups",
                column: "group_type");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_team_id",
                table: "chat_groups",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_team_id_group_type",
                table: "chat_groups",
                columns: new[] { "team_id", "group_type" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chat_group_id",
                table: "chat_messages",
                column: "chat_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chat_group_id_created_at",
                table: "chat_messages",
                columns: new[] { "chat_group_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chat_group_id_sender_user_id",
                table: "chat_messages",
                columns: new[] { "chat_group_id", "sender_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_created_at",
                table: "chat_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_is_revoked",
                table: "chat_messages",
                column: "is_revoked");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_message_type",
                table: "chat_messages",
                column: "message_type");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sender_user_id",
                table: "chat_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_by_id",
                table: "notifications",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_is_read",
                table: "notifications",
                column: "is_read");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id_is_read",
                table: "notifications",
                columns: new[] { "recipient_user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id_type",
                table: "notifications",
                columns: new[] { "recipient_user_id", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_type",
                table: "notifications",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_group_members");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "chat_groups");
        }
    }
}
