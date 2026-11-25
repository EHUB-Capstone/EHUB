using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseStandaloneInsultRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000049"), "Standalone insult / Từ công kích đứng độc lập trong câu", true, "ngu", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000050"), "Personal abuse / Công kích cá nhân", true, "óc bò", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000051"), "Personal abuse / Công kích cá nhân", true, "súc sinh", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000052"), "Personal abuse / Công kích cá nhân", true, "khốn nạn", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000053"), "Personal abuse / Công kích cá nhân", true, "mất dạy", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000054"), "Personal abuse / Công kích cá nhân", true, "vô học", "Harassment" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000049"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000050"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000051"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000052"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000053"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000054"));
        }
    }
}
