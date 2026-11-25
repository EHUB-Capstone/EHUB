using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseViolationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_enabled",
                table: "showcase_moderation_rules",
                newName: "status");
            // Convert every existing row, including custom phrases and disabled default phrases.
            migrationBuilder.Sql("""
                ALTER TABLE showcase_moderation_rules ALTER COLUMN status DROP DEFAULT;
                ALTER TABLE showcase_moderation_rules ALTER COLUMN status TYPE character varying(20)
                    USING (CASE WHEN status THEN 'Active' ELSE 'Inactive' END);
                """);

            migrationBuilder.AddColumn<string>(
                name: "description_en",
                table: "showcase_violation_categories",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description_vi",
                table: "showcase_violation_categories",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "supports_phrase_blocking",
                table: "showcase_violation_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "AdultContent",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Explicit sexual or pornographic content. Educational and health discussions require contextual judgment.", "Nội dung tình dục hoặc khiêu dâm rõ ràng. Các thảo luận giáo dục, y tế cần được đánh giá đúng ngữ cảnh.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Defamation",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Abusive accusations or defamatory language. Keyword matching does not verify facts or determine legal violations.", "Cáo buộc hoặc ngôn từ bôi nhọ mang tính công kích. Bộ lọc không tự xác minh sự thật hay kết luận vi phạm pháp luật.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Insult",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Insults, harassment or personal attacks. Constructive criticism and ordinary disagreement remain allowed.", "Lăng mạ hoặc quấy rối cá nhân. Góp ý xây dựng và ý kiến phản biện bình thường vẫn được phép.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Profanity",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Administrator-approved profane words or phrases. Matching respects word boundaries and explicit Vietnamese accents.", "Từ hoặc cụm từ tục tĩu được Admin phê duyệt. Quy tắc đối chiếu theo ranh giới từ và giữ phân biệt dấu tiếng Việt.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Racism",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Racial or ethnic abuse. Neutral academic discussion must not be blocked simply for mentioning a group.", "Miệt thị hoặc công kích về chủng tộc, dân tộc. Không chặn các thảo luận học thuật trung lập chỉ vì nhắc đến một nhóm người.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Spam",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Repeated comments or unwanted promotions. Legitimate demo links, documentation and feedback are not automatically prohibited.", "Bình luận lặp lại hoặc quảng cáo không phù hợp. Liên kết demo, tài liệu và góp ý hợp lệ không tự động bị cấm.", true });

            migrationBuilder.UpdateData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Threat",
                columns: new[] { "description_en", "description_vi", "supports_phrase_blocking" },
                values: new object[] { "Direct threats or calls for violence. Review the context before adding a phrase to the shared blocklist.", "Cụm từ đe dọa trực tiếp hoặc cổ súy hành vi bạo lực. Người duyệt cần xem ngữ cảnh trước khi bổ sung quy tắc.", true });

            migrationBuilder.InsertData(
                table: "showcase_violation_categories",
                columns: new[] { "code", "description_en", "description_vi", "name_en", "name_vi", "sort_order", "supports_phrase_blocking" },
                values: new object[,]
                {
                    { "Misinformation", "Report suspected false or misleading claims. Reviewers check the evidence; the system does not automatically fact-check content.", "Báo cáo thông tin nghi sai lệch hoặc khẳng định gây hiểu nhầm. Người duyệt kiểm tra căn cứ; hệ thống không tự xác minh đúng sai.", "Misinformation", "Thông tin sai lệch", 9, false },
                    { "OffTopic", "Report comments unrelated to the project or disruptive to discussion. Staff review the conversation before deciding.", "Báo cáo bình luận không liên quan đến dự án hoặc gây gián đoạn trao đổi. Staff xem nội dung cuộc thảo luận trước khi quyết định.", "Off-topic", "Lạc chủ đề", 10, false },
                    { "ProhibitedContent", "Report suspected prohibited goods, drugs, gambling or scams. Staff assess context; this category does not create keyword-blocking rules.", "Báo cáo nội dung nghi quảng bá hàng cấm, chất cấm, cá cược hoặc lừa đảo. Staff đánh giá bằng ngữ cảnh; không chặn bằng từ khóa của nhóm này.", "Prohibited content", "Nội dung cấm", 8, false }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_showcase_moderation_rules_status",
                table: "showcase_moderation_rules",
                sql: "status IN ('Active', 'Inactive')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The older version has seven categories. Preserve reports while clearing unsupported category detail.
            migrationBuilder.Sql("UPDATE showcase_reports SET category = NULL WHERE category IN ('ProhibitedContent', 'Misinformation', 'OffTopic');");
            migrationBuilder.DropCheckConstraint(
                name: "ck_showcase_moderation_rules_status",
                table: "showcase_moderation_rules");

            migrationBuilder.DeleteData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "Misinformation");

            migrationBuilder.DeleteData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "OffTopic");

            migrationBuilder.DeleteData(
                table: "showcase_violation_categories",
                keyColumn: "code",
                keyValue: "ProhibitedContent");

            migrationBuilder.DropColumn(
                name: "description_en",
                table: "showcase_violation_categories");

            migrationBuilder.DropColumn(
                name: "description_vi",
                table: "showcase_violation_categories");

            migrationBuilder.DropColumn(
                name: "supports_phrase_blocking",
                table: "showcase_violation_categories");

            migrationBuilder.Sql("""
                ALTER TABLE showcase_moderation_rules ALTER COLUMN status DROP DEFAULT;
                ALTER TABLE showcase_moderation_rules ALTER COLUMN status TYPE boolean USING (status = 'Active');
                """);
            migrationBuilder.RenameColumn(
                name: "status",
                table: "showcase_moderation_rules",
                newName: "is_enabled");

        }
    }
}
