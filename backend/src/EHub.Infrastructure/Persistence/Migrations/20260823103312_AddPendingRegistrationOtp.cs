using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingRegistrationOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Accounts created before registration OTP was introduced already
            // proved ownership through the legacy registration/login flow. Keep
            // them usable; only registrations created after this migration must
            // complete the new verification challenge.
            migrationBuilder.Sql("""
                UPDATE users
                SET is_email_verified = TRUE
                WHERE is_email_verified = FALSE;
                """);

            migrationBuilder.CreateTable(
                name: "pending_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    major_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    otp_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    otp_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failed_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    resend_count = table.Column<int>(type: "integer", nullable: false),
                    last_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_pending_registrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_completed_user_id",
                table: "pending_registrations",
                column: "completed_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_normalized_email",
                table: "pending_registrations",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_status_otp_expires_at_utc",
                table: "pending_registrations",
                columns: new[] { "status", "otp_expires_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_registrations");
        }
    }
}
