using System;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ClassStudent
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public string? MemberCode { get; set; }

    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;

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
}
