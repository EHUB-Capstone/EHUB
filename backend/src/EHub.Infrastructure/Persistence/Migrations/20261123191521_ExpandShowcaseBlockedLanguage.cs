using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandShowcaseBlockedLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000005"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "cdmm", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000006"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "clm", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000007"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "clgt", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000008"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "óc cặc", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000009"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "óc loz", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000010"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "oc lon", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000011"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "fuck", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000012"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "chó đẻ", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000013"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "dume", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000014"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "duma", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000015"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "đỉ mẹ", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000016"), "Possible personal insult / Có dấu hiệu công kích cá nhân", true, "địt mẹ", "Harassment" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000005"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000006"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000007"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000008"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000009"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000010"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000011"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000012"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000013"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000014"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000015"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000016"));
        }
    }
}
