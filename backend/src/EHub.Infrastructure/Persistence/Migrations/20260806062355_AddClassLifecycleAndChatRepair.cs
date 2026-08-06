using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassLifecycleAndChatRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_groups_class_id_group_type",
                table: "chat_groups");

            migrationBuilder.DropIndex(
                name: "IX_chat_groups_team_id_group_type",
                table: "chat_groups");

            migrationBuilder.DropIndex(
                name: "IX_chat_group_members_chat_group_id_student_id",
                table: "chat_group_members");

            migrationBuilder.DropIndex(
                name: "IX_chat_group_members_chat_group_id_user_id",
                table: "chat_group_members");

            migrationBuilder.AddColumn<string>(
                name: "status_before_archive",
                table: "classes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_read_only",
                table: "chat_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Normalize legacy rows before enforcing the one-group/one-membership invariants.
            // Records are soft-deleted so a rollback or forensic inspection remains possible.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY class_id, group_type
                               ORDER BY created_at, id) AS position
                    FROM chat_groups
                    WHERE team_id IS NULL AND group_type = 'ClassGroup' AND is_deleted = false
                )
                UPDATE chat_groups AS target
                SET is_deleted = true, deleted_at = NOW()
                FROM ranked
                WHERE target.id = ranked.id AND ranked.position > 1;

                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY team_id, group_type
                               ORDER BY created_at, id) AS position
                    FROM chat_groups
                    WHERE team_id IS NOT NULL AND group_type = 'TeamGroup' AND is_deleted = false
                )
                UPDATE chat_groups AS target
                SET is_deleted = true, deleted_at = NOW()
                FROM ranked
                WHERE target.id = ranked.id AND ranked.position > 1;

                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY chat_group_id, user_id
                               ORDER BY is_active DESC, joined_at DESC, id) AS position
                    FROM chat_group_members
                    WHERE user_id IS NOT NULL AND is_deleted = false
                )
                UPDATE chat_group_members AS target
                SET is_deleted = true, deleted_at = NOW(), is_active = false, left_at = COALESCE(left_at, NOW())
                FROM ranked
                WHERE target.id = ranked.id AND ranked.position > 1;

                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY chat_group_id, student_id
                               ORDER BY is_active DESC, joined_at DESC, id) AS position
                    FROM chat_group_members
                    WHERE student_id IS NOT NULL AND is_deleted = false
                )
                UPDATE chat_group_members AS target
                SET is_deleted = true, deleted_at = NOW(), is_active = false, left_at = COALESCE(left_at, NOW())
                FROM ranked
                WHERE target.id = ranked.id AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_class_id_group_type",
                table: "chat_groups",
                columns: new[] { "class_id", "group_type" },
                unique: true,
                filter: "team_id IS NULL AND group_type = 'ClassGroup' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_team_id_group_type",
                table: "chat_groups",
                columns: new[] { "team_id", "group_type" },
                unique: true,
                filter: "team_id IS NOT NULL AND group_type = 'TeamGroup' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_student_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "student_id" },
                unique: true,
                filter: "student_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_user_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_groups_class_id_group_type",
                table: "chat_groups");

            migrationBuilder.DropIndex(
                name: "IX_chat_groups_team_id_group_type",
                table: "chat_groups");

            migrationBuilder.DropIndex(
                name: "IX_chat_group_members_chat_group_id_student_id",
                table: "chat_group_members");

            migrationBuilder.DropIndex(
                name: "IX_chat_group_members_chat_group_id_user_id",
                table: "chat_group_members");

            migrationBuilder.DropColumn(
                name: "status_before_archive",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "is_read_only",
                table: "chat_groups");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_class_id_group_type",
                table: "chat_groups",
                columns: new[] { "class_id", "group_type" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_team_id_group_type",
                table: "chat_groups",
                columns: new[] { "team_id", "group_type" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_student_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_group_members_chat_group_id_user_id",
                table: "chat_group_members",
                columns: new[] { "chat_group_id", "user_id" });
        }
    }
}
