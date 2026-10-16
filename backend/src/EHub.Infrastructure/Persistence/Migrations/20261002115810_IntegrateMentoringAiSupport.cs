using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateMentoringAiSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "cv_file_name",
                table: "mentor_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cv_public_id",
                table: "mentor_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cv_storage_url",
                table: "mentor_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "experience",
                table: "mentor_profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_file_name",
                table: "mentor_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_public_id",
                table: "mentor_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_storage_url",
                table: "mentor_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_url",
                table: "mentor_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mentor_embeddings",
                columns: table => new
                {
                    mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_embeddings", x => x.mentor_profile_id);
                    table.ForeignKey(
                        name: "FK_mentor_embeddings_mentor_profiles_mentor_profile_id",
                        column: x => x.mentor_profile_id,
                        principalTable: "mentor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mentoring_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentoring_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_mentoring_feedback", x => x.id);
                    table.CheckConstraint("CK_MentoringFeedback_Rating", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "FK_mentoring_feedback_mentoring_sessions_mentoring_session_id",
                        column: x => x.mentoring_session_id,
                        principalTable: "mentoring_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mentoring_feedback_users_student_user_id",
                        column: x => x.student_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_feedback_mentoring_session_id_student_user_id",
                table: "mentoring_feedback",
                columns: new[] { "mentoring_session_id", "student_user_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_mentoring_feedback_student_user_id",
                table: "mentoring_feedback",
                column: "student_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_embeddings");

            migrationBuilder.DropTable(
                name: "mentoring_feedback");

            migrationBuilder.DropColumn(
                name: "cv_file_name",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "cv_public_id",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "cv_storage_url",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "experience",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "portfolio_file_name",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "portfolio_public_id",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "portfolio_storage_url",
                table: "mentor_profiles");

            migrationBuilder.DropColumn(
                name: "portfolio_url",
                table: "mentor_profiles");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
