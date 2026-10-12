using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileConvertedTemporaryMentors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only cleanup. Earlier versions turned a mentor without an account into a real mentor when the email arrived,
            // but left the semester entry of the mentor without an account behind, so the person appeared twice. For every such
            // mentor: make sure the account is listed in each semester (same Active/Inactive status), then retire the old entry.
            // Safe to run more than once: nothing is added when the account is already listed.
            migrationBuilder.Sql(@"
INSERT INTO semester_staff_assignments (id, semester_id, user_id, role, status, created_at, is_deleted)
SELECT md5(random()::text || clock_timestamp()::text || c.semester_id::text || c.user_id::text)::uuid,
       c.semester_id, c.user_id, 'Mentor',
       CASE WHEN bool_or(c.status = 'Active') THEN 'Active' ELSE 'Inactive' END,
       now(), false
FROM (
    SELECT t.semester_id, p.user_id, t.status
    FROM semester_temporary_mentors t
    JOIN mentor_import_drafts d ON d.id = t.draft_id
    JOIN mentor_profiles p ON p.id = d.converted_mentor_profile_id
    WHERE t.is_deleted = false AND d.status = 'Converted'
) c
GROUP BY c.semester_id, c.user_id
HAVING NOT EXISTS (
    SELECT 1 FROM semester_staff_assignments s
    WHERE s.semester_id = c.semester_id AND s.user_id = c.user_id AND s.role = 'Mentor' AND s.is_deleted = false);");

            migrationBuilder.Sql(@"
UPDATE semester_staff_assignments s
SET status = 'Active', updated_at = now()
WHERE s.role = 'Mentor' AND s.status = 'Inactive' AND s.is_deleted = false
  AND EXISTS (
    SELECT 1
    FROM semester_temporary_mentors t
    JOIN mentor_import_drafts d ON d.id = t.draft_id
    JOIN mentor_profiles p ON p.id = d.converted_mentor_profile_id
    WHERE t.is_deleted = false AND d.status = 'Converted' AND t.status = 'Active'
      AND t.semester_id = s.semester_id AND p.user_id = s.user_id);");

            migrationBuilder.Sql(@"
UPDATE semester_temporary_mentors t
SET is_deleted = true, deleted_at = now()
WHERE t.is_deleted = false
  AND EXISTS (
    SELECT 1
    FROM mentor_import_drafts d
    JOIN mentor_profiles p ON p.id = d.converted_mentor_profile_id
    WHERE d.id = t.draft_id AND d.status = 'Converted');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the cleanup only moved duplicate semester entries onto the mentor accounts. The accounts
            // that were added are valid data, and the retired duplicates (soft-deleted) are not restored. Back up the database
            // before deploying if a full rollback must be possible.
        }
    }
}
