using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseReportAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_snapshot",
                table: "showcase_reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at_utc",
                table: "showcase_moderation_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "showcase_moderation_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "showcase_moderation_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                table: "showcase_moderation_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "showcase_moderation_rule_proposals",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rules_created_by_user_id",
                table: "showcase_moderation_rules",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rules_updated_by_user_id",
                table: "showcase_moderation_rules",
                column: "updated_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_showcase_moderation_rules_users_created_by_user_id",
                table: "showcase_moderation_rules",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_showcase_moderation_rules_users_updated_by_user_id",
                table: "showcase_moderation_rules",
                column: "updated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_showcase_moderation_rules_users_created_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_showcase_moderation_rules_users_updated_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropIndex(
                name: "IX_showcase_moderation_rules_created_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropIndex(
                name: "IX_showcase_moderation_rules_updated_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "content_snapshot",
                table: "showcase_reports");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "showcase_moderation_rule_proposals");
        }
    }
}
