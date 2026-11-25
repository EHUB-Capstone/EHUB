using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseStandaloneProfanityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000055"), "Standalone profanity / Từ tục tĩu đứng độc lập trong câu", true, "cặc", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000056"), "Standalone profanity / Từ tục tĩu đứng độc lập trong câu", true, "loz", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000057"), "Standalone profanity / Từ tục tĩu đứng độc lập trong câu", true, "lồn", "Harassment" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000055"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000056"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000057"));
        }
    }
}
