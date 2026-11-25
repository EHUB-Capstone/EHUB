using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPigInsultTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "showcase_moderation_rules",
                columns: new[] { "id", "category", "description", "is_enabled", "phrase", "reason" },
                values: new object[,]
                {
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000058"), "Insult", "Standalone insult / Từ bị chặn theo quy tắc cộng đồng", true, "lợn", "Harassment" },
                    { new Guid("946a4c91-263f-4cea-a3aa-000000000059"), "Insult", "Standalone insult / Từ bị chặn theo quy tắc cộng đồng", true, "heo", "Harassment" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000058"));

            migrationBuilder.DeleteData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000059"));
        }
    }
}
