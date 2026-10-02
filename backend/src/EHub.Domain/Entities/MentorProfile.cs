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
    public string? Bio { get; set; }
    public string? Organization { get; set; }
    public string? LinkedInUrl { get; set; }
    public string MentorType
    {
        get => Type == EHub.Domain.Enums.MentorType.Enterprise ? "Business" : "IT";
        set => Type = value == "IT" ? EHub.Domain.Enums.MentorType.Academic : EHub.Domain.Enums.MentorType.Enterprise;
    }
    public string? Experience { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? CvStorageUrl { get; set; }
    public string? CvPublicId { get; set; }
    public string? CvFileName { get; set; }
    public string? PortfolioStorageUrl { get; set; }
    public string? PortfolioPublicId { get; set; }
    public string? PortfolioFileName { get; set; }

    public MentorType Type { get; set; } = EHub.Domain.Enums.MentorType.Enterprise;
    public DateOnly? DateOfBirth { get; set; }
    public string? ContractType { get; set; }
    public string? EducationLevel { get; set; }
    public string? CurrentAddress { get; set; }
    public string? FptEmail { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }

    public MentorProfileStatus Status { get; set; } = MentorProfileStatus.Active;

    // Navigation properties
    public virtual ICollection<MentorAssignment> Assignments { get; set; } = new List<MentorAssignment>();
}
