using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseThreatAndAdultLanguageRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000017"), "Threat / Đe dọa trực tiếp", true, "tao sẽ giết mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000018"), "Threat / Đe dọa trực tiếp", true, "tao giết mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000019"), "Threat / Đe dọa trực tiếp", true, "giết cả nhà mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000020"), "Threat / Đe dọa trực tiếp", true, "tao sẽ đánh mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000021"), "Threat / Đe dọa trực tiếp", true, "đập chết mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000022"), "Threat / Đe dọa trực tiếp", true, "tao sẽ đốt nhà mày", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000023"), "Threat / Đe dọa trực tiếp", true, "i will kill you", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000024"), "Threat / Đe dọa trực tiếp", true, "i will hurt you", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000025"), "Personal abuse / Công kích cá nhân", true, "đồ súc sinh", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000026"), "Personal abuse / Công kích cá nhân", true, "đồ khốn nạn", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000027"), "Personal abuse / Công kích cá nhân", true, "đồ rác rưởi", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000028"), "Personal abuse / Công kích cá nhân", true, "đồ vô học", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000029"), "Personal abuse / Công kích cá nhân", true, "đồ mất dạy", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000030"), "Personal abuse / Công kích cá nhân", true, "mày là rác rưởi", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000031"), "Abusive accusation / Cáo buộc mang tính công kích", true, "đồ lừa đảo", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000032"), "Abusive accusation / Cáo buộc mang tính công kích", true, "lũ lừa đảo", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000033"), "Abusive accusation / Cáo buộc mang tính công kích", true, "mày là đồ ăn cắp", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000034"), "Abusive accusation / Cáo buộc mang tính công kích", true, "lũ ăn cắp", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000035"), "Racial abuse / Miệt thị chủng tộc", true, "lũ da đen", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000036"), "Racial abuse / Miệt thị chủng tộc", true, "lũ da trắng", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000037"), "Racial abuse / Miệt thị chủng tộc", true, "đồ mọi rợ", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000038"), "Racial abuse / Miệt thị chủng tộc", true, "lũ mọi", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000039"), "Racial abuse / Miệt thị chủng tộc", true, "chủng tộc hạ đẳng", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000040"), "Racial abuse / Miệt thị chủng tộc", true, "racially inferior", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000041"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "phim sex", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000042"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "video sex", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000043"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "ảnh khiêu dâm", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000044"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "ảnh khỏa thân", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000045"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "mua dâm", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000046"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "bán dâm", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000047"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "dịch vụ mại dâm", "InappropriateContent" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000048"), "Explicit adult content / Nội dung người lớn không phù hợp", true, "gái gọi", "InappropriateContent" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000017"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000018"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000019"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000020"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000021"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000022"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000023"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000024"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000025"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000026"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000027"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000028"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000029"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000030"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000031"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000032"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000033"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000034"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000035"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000036"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000037"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000038"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000039"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000040"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000041"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000042"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000043"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000044"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000045"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000046"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000047"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000048"));
        }
    }
}
