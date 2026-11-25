using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseViolationCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "showcase_moderation_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Insult");

            // Backfill custom rules by the existing broad reason before categorizing the seeded catalog.
            // This preserves their phrase, description and enabled state.
            migrationBuilder.Sql("""
                UPDATE showcase_moderation_rules
                SET category = CASE reason
                    WHEN 'Spam' THEN 'Spam'
                    WHEN 'InappropriateContent' THEN 'AdultContent'
                    ELSE 'Insult'
                END;
                """);

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000001"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000002"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000003"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000004"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000005"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000006"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000007"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000008"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000009"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000010"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000011"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000012"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000013"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000014"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000015"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000016"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000017"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000018"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000019"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000020"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000021"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000022"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000023"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000024"),
                column: "category",
                value: "Threat");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000025"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000026"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000027"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000028"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000029"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000030"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000031"),
                column: "category",
                value: "Defamation");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000032"),
                column: "category",
                value: "Defamation");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000033"),
                column: "category",
                value: "Defamation");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000034"),
                column: "category",
                value: "Defamation");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000035"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000036"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000037"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000038"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000039"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000040"),
                column: "category",
                value: "Racism");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000041"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000042"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000043"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000044"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000045"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000046"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000047"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000048"),
                column: "category",
                value: "AdultContent");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000049"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000050"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000051"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000052"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000053"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000054"),
                column: "category",
                value: "Insult");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000055"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000056"),
                column: "category",
                value: "Profanity");

            migrationBuilder.UpdateData(
                table: "showcase_moderation_rules",
                keyColumn: "id",
                keyValue: new Guid("946a4c91-263f-4cea-a3aa-000000000057"),
                column: "category",
                value: "Profanity");

            migrationBuilder.AddCheckConstraint(
                name: "ck_showcase_moderation_rules_category",
                table: "showcase_moderation_rules",
                sql: "category IN ('Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_showcase_moderation_rules_category",
                table: "showcase_moderation_rules");

            migrationBuilder.DropColumn(
                name: "category",
                table: "showcase_moderation_rules");
        }
    }
}
