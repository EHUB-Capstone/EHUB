using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260926090000_RemoveSampleMentorSeed")]
public partial class RemoveSampleMentorSeed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE mentor_assignments
            SET status = 'Ended',
                ended_at = COALESCE(ended_at, CURRENT_TIMESTAMP),
                updated_at = CURRENT_TIMESTAMP
            WHERE mentor_profile_id IN (
                SELECT mp.id
                FROM mentor_profiles mp
                INNER JOIN users u ON u.id = mp.user_id
                WHERE LOWER(u.email) = 'mentor.sample@ehub.edu.vn'
                  AND u.full_name = 'Dr. John Doe (Sample Mentor)'
            )
              AND status = 'Active'
              AND is_deleted = false;

            UPDATE semester_staff_assignments
            SET status = 'Inactive',
                updated_at = CURRENT_TIMESTAMP
            WHERE user_id IN (
                SELECT id
                FROM users
                WHERE LOWER(email) = 'mentor.sample@ehub.edu.vn'
                  AND full_name = 'Dr. John Doe (Sample Mentor)'
            )
              AND status = 'Active'
              AND is_deleted = false;

            UPDATE mentor_profiles
            SET status = 'Inactive',
                is_deleted = true,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE user_id IN (
                SELECT id
                FROM users
                WHERE LOWER(email) = 'mentor.sample@ehub.edu.vn'
                  AND full_name = 'Dr. John Doe (Sample Mentor)'
            )
              AND is_deleted = false;

            UPDATE users
            SET status = 'Inactive',
                is_deleted = true,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE LOWER(email) = 'mentor.sample@ehub.edu.vn'
              AND full_name = 'Dr. John Doe (Sample Mentor)'
              AND is_deleted = false;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentional no-op: rolling back code must not recreate a removed sample account.
    }
}
