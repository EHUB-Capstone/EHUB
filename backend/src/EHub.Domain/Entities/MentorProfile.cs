using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class MentorProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string[] Expertise { get; set; } = Array.Empty<string>();
    // Background of the mentor: experience, achievements, areas they can coach.
    public string? Bio { get; set; }
    // Free-text availability, for example "Weekday afternoons, online only". Status says whether the mentor can be assigned at all.
    public string? AvailabilityNote { get; set; }
    public string? Organization { get; set; }
    public string? LinkedInUrl { get; set; }

    public MentorType Type { get; set; } = MentorType.Enterprise;
    public DateOnly? DateOfBirth { get; set; }
    public string? ContractType { get; set; }
    public string? EducationLevel { get; set; }
    public string? CurrentAddress { get; set; }
    public string? FptEmail { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }

    public MentorProfileStatus Status { get; set; } = MentorProfileStatus.Active;

    // PostgreSQL xmin: lets two admins editing the same profile be detected instead of overwriting each other.
    public uint Version { get; set; }

    // Navigation properties
    public virtual ICollection<MentorAssignment> Assignments { get; set; } = new List<MentorAssignment>();
}
