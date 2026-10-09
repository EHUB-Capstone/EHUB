using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class MentorProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    // What the mentor can coach (for example Marketing, Fundraising).
    public string[] Expertise { get; set; } = Array.Empty<string>();
    // Startup fields the mentor knows (for example FinTech, EdTech).
    public string[] StartupDomains { get; set; } = Array.Empty<string>();
    // Technologies the mentor works with (for example React, .NET, Machine learning).
    public string[] TechnologySkills { get; set; } = Array.Empty<string>();
    // Free labels used to group or find mentors.
    public string[] MentorTags { get; set; } = Array.Empty<string>();
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
