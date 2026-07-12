using System;

namespace EHub.Domain.Entities;

public class ClassLecturer
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid LecturerId { get; set; }
    public virtual User Lecturer { get; set; } = null!;

    public bool IsPrimary { get; set; } = true;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Guid? AssignedById { get; set; }
    public virtual User? AssignedBy { get; set; }
}
