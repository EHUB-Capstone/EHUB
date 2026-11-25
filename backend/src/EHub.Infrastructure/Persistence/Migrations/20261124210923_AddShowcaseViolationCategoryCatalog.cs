using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseViolationCategoryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "showcase_reports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "showcase_violation_categories",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_vi = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_showcase_violation_categories", x => x.code);
                });

            migrationBuilder.InsertData(
                table: "showcase_violation_categories",
                columns: new[] { "code", "name_en", "name_vi", "sort_order" },
                values: new object[,]
                {
                    { "AdultContent", "Explicit adult content", "Nội dung người lớn", 6 },
                    { "Defamation", "Abusive accusations and defamation", "Cáo buộc công kích, bôi nhọ", 4 },
                    { "Insult", "Insults and personal attacks", "Xúc phạm, công kích cá nhân", 2 },
                    { "Profanity", "Profanity", "Tục tĩu", 1 },
                    { "Racism", "Racism and racial abuse", "Miệt thị chủng tộc", 5 },
                    { "Spam", "Spam and prohibited promotions", "Spam, quảng cáo bị cấm", 7 },
                    { "Threat", "Threats and incitement to violence", "Đe dọa, kích động bạo lực", 3 }
                });

            // Preserve legacy reasons. Copyright/Other have no exact equivalent in the seven language groups.
            migrationBuilder.Sql("""
                UPDATE showcase_reports
                SET category = CASE reason
                    WHEN 'Spam' THEN 'Spam'
                    WHEN 'Harassment' THEN 'Insult'
                    WHEN 'InappropriateContent' THEN 'AdultContent'
                    ELSE NULL
                END
                WHERE category IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_showcase_reports_category",
                table: "showcase_reports",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rules_category",
                table: "showcase_moderation_rules",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_showcase_moderation_rule_proposals_category",
                table: "showcase_moderation_rule_proposals",
                column: "category");

            migrationBuilder.AddForeignKey(
                name: "FK_showcase_moderation_rule_proposals_showcase_violation_categ~",
                table: "showcase_moderation_rule_proposals",
                column: "category",
                principalTable: "showcase_violation_categories",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_showcase_moderation_rules_showcase_violation_categories_cat~",
                table: "showcase_moderation_rules",
                column: "category",
                principalTable: "showcase_violation_categories",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_showcase_reports_showcase_violation_categories_category",
                table: "showcase_reports",
                column: "category",
                principalTable: "showcase_violation_categories",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_showcase_moderation_rule_proposals_showcase_violation_categ~",
                table: "showcase_moderation_rule_proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_showcase_moderation_rules_showcase_violation_categories_cat~",
                table: "showcase_moderation_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_showcase_reports_showcase_violation_categories_category",
                table: "showcase_reports");

            migrationBuilder.DropTable(
                name: "showcase_violation_categories");

            migrationBuilder.DropIndex(
                name: "IX_showcase_reports_category",
                table: "showcase_reports");

            migrationBuilder.DropIndex(
                name: "IX_showcase_moderation_rules_category",
                table: "showcase_moderation_rules");

            migrationBuilder.DropIndex(
                name: "IX_showcase_moderation_rule_proposals_category",
                table: "showcase_moderation_rule_proposals");

            migrationBuilder.DropColumn(
                name: "category",
                table: "showcase_reports");
        }
    }
}
