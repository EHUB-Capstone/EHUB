using EHub.Contracts.Users;

namespace EHub.Contracts.Mentors;

public sealed class MentorImportRowPreview
{
    public int RowNumber { get; init; }
    public string SheetName { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? FptEmail { get; init; }
    public string? Phone { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? ContractType { get; init; }
    public string? EducationLevel { get; init; }
    public string? CurrentAddress { get; init; }
    public string? Organization { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyCollection<string> MissingFields { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}

public sealed class MentorImportPreviewResponse
{
    public Guid SessionId { get; init; }
    public int TotalRows { get; init; }
    public int CreateCount { get; init; }
    public int UpdateCount { get; init; }
    public int NeedsCompletionCount { get; init; }
    public int CompleteDraftCount { get; init; }
    public int ErrorCount { get; init; }
    public bool CanCommit { get; init; }
    public IReadOnlyCollection<MentorImportRowPreview> Rows { get; init; } = Array.Empty<MentorImportRowPreview>();
}

public sealed class CommitMentorImportRequest
{
    public Guid SessionId { get; init; }
}

public sealed class MentorImportCommitResponse
{
    public int CreatedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int DraftSavedCount { get; init; }
    public int DraftCompletedCount { get; init; }
    // Teams whose temporary mentor became the real mentor because the email arrived.
    public int TemporaryAssignmentsConverted { get; init; }
}

// A mentor kept in the master list without a login account yet (for example the workbook had no email).
public sealed class IncompleteMentorResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public string? Email { get; init; }
    public IReadOnlyCollection<string> MissingFields { get; init; } = Array.Empty<string>();
    // Teams that currently use this mentor as a temporary mentor; they stay without an account until an email arrives.
    public int ActiveTeamCount { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class IncompleteMentorListResponse
{
    public IReadOnlyCollection<IncompleteMentorResponse> Mentors { get; init; } = Array.Empty<IncompleteMentorResponse>();
    public PaginationResponse Pagination { get; init; } = new();
}

public static class MentorAllocationStrategies
{
    public const string Balanced = "Balanced";
    public const string Random = "Random";
}

// A hand edit of one team slot in the allocation preview.
// MentorProfileId set: put that mentor in the slot. MentorProfileId null: leave the slot empty.
// Replace: explicitly end the mentor currently in the slot (a Reason is then required) instead of keeping them.
public sealed class MentorAllocationEdit
{
    public Guid TeamId { get; init; }
    public string MentorType { get; init; } = string.Empty;
    public Guid? MentorProfileId { get; init; }
    public bool Replace { get; init; }
    public string? Reason { get; init; }
}

public sealed class PreviewMentorAllocationRequest
{
    public Guid SemesterId { get; init; }
    public IReadOnlyCollection<Guid> ClassIds { get; init; } = Array.Empty<Guid>();
    public int? Seed { get; init; }
    // Mentors without an account that take part in the semester can be proposed too. Turn off to use registered mentors only.
    public bool IncludeTemporaryMentors { get; init; } = true;
    // "Balanced" (default) or "Random".
    public string Strategy { get; init; } = MentorAllocationStrategies.Balanced;
    public IReadOnlyCollection<MentorAllocationEdit> Edits { get; init; } = Array.Empty<MentorAllocationEdit>();
}

public sealed class CommitMentorAllocationRequest
{
    public Guid SessionId { get; init; }
}

public sealed class MentorAllocationRowPreview
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public Guid MentorProfileId { get; init; }
    // True when MentorProfileId is the id of a mentor without an account (an incomplete-mentor record).
    public bool IsTemporary { get; init; }
    public string MentorName { get; init; } = string.Empty;
    public string MentorEmail { get; init; } = string.Empty;
    public int ResultingSemesterLoad { get; init; }
    // "Retained" keeps the mentor of the previous semester's team; "Allocated" is a newly chosen mentor.
    public string Source { get; init; } = MentorAllocationSources.Allocated;
    // Set when this row replaces the mentor currently in the slot. The old assignment is ended when the preview is confirmed.
    public Guid? ReplacesAssignmentId { get; init; }
    public Guid? ReplacesMentorProfileId { get; init; }
    public string? ReplacesMentorName { get; init; }
    public string? ReplaceReason { get; init; }
    // Teams the mentor carried when the preview was generated; used to detect that data changed before confirming.
    public int? MentorLoadBefore { get; init; }
}

public static class MentorAllocationSources
{
    public const string Allocated = "Allocated";
    public const string Retained = "Retained";
    public const string Manual = "Manual";
}

// A previous mentor that could not be carried over to a continuing team, with the reason.
public sealed class MentorAllocationSkippedPreview
{
    public Guid? TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string SourceTeamCode { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public Guid MentorProfileId { get; init; }
    public string MentorName { get; init; } = string.Empty;
    public string MentorEmail { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

// A team slot that still has no mentor after the proposed allocation (for example no mentor of that type is active).
public sealed class MentorAllocationUnfilledPreview
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
}

public sealed class MentorAllocationSubjectLoad
{
    public string SubjectCode { get; init; } = string.Empty;
    public int Before { get; init; }
    public int Added { get; init; }
    // Assignments that end because the admin replaced the mentor in a slot.
    public int Removed { get; init; }
}

// Teams carried by one active mentor of the semester, before and after the proposed allocation.
public sealed class MentorAllocationMentorLoad
{
    public Guid MentorProfileId { get; init; }
    public string MentorName { get; init; } = string.Empty;
    public string MentorEmail { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public string? ContractType { get; init; }
    public IReadOnlyCollection<MentorAllocationSubjectLoad> Subjects { get; init; } = Array.Empty<MentorAllocationSubjectLoad>();
    public int TotalBefore { get; init; }
    public int TotalAfter { get; init; }
}

// A hand edit that was not applied, or a slot where the proposal meets a different mentor already assigned.
public sealed class MentorAllocationConflictPreview
{
    public Guid? TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public Guid? CurrentAssignmentId { get; init; }
    public Guid? CurrentMentorProfileId { get; init; }
    public string? CurrentMentorName { get; init; }
    public Guid? ProposedMentorProfileId { get; init; }
    public string? ProposedMentorName { get; init; }
    // "SlotOccupied" (resubmit the edit with Replace to swap) or "EditRejected".
    public string Kind { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

// A mentor already assigned to a team slot. Replaced is true when the preview ends this assignment in favour of a new mentor.
public sealed class MentorAllocationExistingPreview
{
    public Guid AssignmentId { get; init; }
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public Guid MentorProfileId { get; init; }
    public string MentorName { get; init; } = string.Empty;
    public bool Replaced { get; init; }
}

public sealed class MentorAllocationPreviewResponse
{
    public Guid SessionId { get; init; }
    public Guid SemesterId { get; init; }
    public int Seed { get; init; }
    public int TeamCount { get; init; }
    public int MissingEnterpriseCount { get; init; }
    public int MissingAcademicCount { get; init; }
    public int RetainedCount { get; init; }
    public string Strategy { get; init; } = MentorAllocationStrategies.Balanced;
    public int UnfilledEnterpriseCount { get; init; }
    public int UnfilledAcademicCount { get; init; }
    public bool CanCommit { get; init; }
    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<MentorAllocationRowPreview> Assignments { get; init; } = Array.Empty<MentorAllocationRowPreview>();
    public IReadOnlyCollection<MentorAllocationSkippedPreview> Skipped { get; init; } = Array.Empty<MentorAllocationSkippedPreview>();
    public IReadOnlyCollection<MentorAllocationUnfilledPreview> Unfilled { get; init; } = Array.Empty<MentorAllocationUnfilledPreview>();
    public IReadOnlyCollection<MentorAllocationMentorLoad> MentorLoads { get; init; } = Array.Empty<MentorAllocationMentorLoad>();
    public IReadOnlyCollection<MentorAllocationConflictPreview> Conflicts { get; init; } = Array.Empty<MentorAllocationConflictPreview>();
    public IReadOnlyCollection<MentorAllocationExistingPreview> ExistingAssignments { get; init; } = Array.Empty<MentorAllocationExistingPreview>();
    public int ReplacementCount { get; init; }
}

public sealed class MentorAllocationCommitResponse
{
    public int EndedCount { get; init; }
    public int CreatedCount { get; init; }
    public int SkippedCount { get; init; }
}
