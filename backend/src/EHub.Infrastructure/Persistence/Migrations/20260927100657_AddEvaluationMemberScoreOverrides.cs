using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationMemberScoreOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_member_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_member_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_member_scores_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_member_scores_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_member_scores_evaluation_id_student_id",
                table: "evaluation_member_scores",
                columns: new[] { "evaluation_id", "student_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_member_scores_student_id",
                table: "evaluation_member_scores",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_member_scores");
        }
    }
}
