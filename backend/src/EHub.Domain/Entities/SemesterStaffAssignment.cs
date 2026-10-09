using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class SemesterStaffAssignment : AuditableEntity
{
    public Guid SemesterId { get; set; }
    public virtual Semester Semester { get; set; } = null!;

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public SemesterStaffRole Role { get; set; }
    public SemesterStaffStatus Status { get; set; } = SemesterStaffStatus.Active;

    public uint Version { get; set; }
}
