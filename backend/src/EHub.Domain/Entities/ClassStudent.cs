using System;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ClassStudent
{
    public uint Version { get; set; }

    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    // Denormalized immutable enrollment scope. It lets PostgreSQL enforce that
    // a student cannot have two active enrollments for one course in a semester.
    public Guid SemesterId { get; set; }
    public Guid CourseId { get; set; }

    public string? MemberCode { get; set; }

    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;
    public bool CountsTowardCourseSemesterLimit { get; set; } = true;

    // Enrollment snapshot: lecturers may correct/verify this value without
    // mutating the student's global profile major.
    public string MajorCodeAtEnrollment { get; set; } = string.Empty;
    public EnrollmentMajorVerificationStatus MajorVerificationStatus { get; set; } = EnrollmentMajorVerificationStatus.Unverified;
    public DateTime? MajorVerifiedAtUtc { get; set; }
    public Guid? MajorVerifiedByUserId { get; set; }
    public virtual User? MajorVerifiedByUser { get; set; }

    public DateTime? ExamDate { get; set; }
    public string? ExamNote { get; set; }

    public string? Outcome1 { get; set; }
    public string? Outcome1Comment { get; set; }

    public string? Outcome2 { get; set; }
    public string? Outcome2Comment { get; set; }

    public string? Outcome3 { get; set; }
    public string? Outcome3Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
