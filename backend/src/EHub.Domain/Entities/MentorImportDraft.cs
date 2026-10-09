using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class MentorImportDraft : AuditableEntity
{
    // Null for an incomplete mentor kept in the master list, which belongs to no semester.
    public Guid? SemesterId { get; set; }
    public Semester? Semester { get; set; }

    public MentorType Type { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string NormalizedFullName { get; set; } = string.Empty;
    public string? SourceOrdinal { get; set; }
    public string? Email { get; set; }
    public string? FptEmail { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? ContractType { get; set; }
    public string? EducationLevel { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Organization { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }

    public MentorImportDraftStatus Status { get; set; } = MentorImportDraftStatus.NeedsCompletion;
    public Guid? ConvertedMentorProfileId { get; set; }
    public MentorProfile? ConvertedMentorProfile { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
}
