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
    public string Status { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}

public sealed class MentorImportPreviewResponse
{
    public Guid SessionId { get; init; }
    public Guid SemesterId { get; init; }
    public int TotalRows { get; init; }
    public int CreateCount { get; init; }
    public int UpdateCount { get; init; }
    public int AddToSemesterCount { get; init; }
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
    public int SemesterAssignmentCount { get; init; }
}

public sealed class PreviewMentorAllocationRequest
{
    public Guid SemesterId { get; init; }
    public IReadOnlyCollection<Guid> ClassIds { get; init; } = Array.Empty<Guid>();
    public int? Seed { get; init; }
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
    public string MentorName { get; init; } = string.Empty;
    public string MentorEmail { get; init; } = string.Empty;
    public int ResultingSemesterLoad { get; init; }
}

public sealed class MentorAllocationPreviewResponse
{
    public Guid SessionId { get; init; }
    public Guid SemesterId { get; init; }
    public int Seed { get; init; }
    public int TeamCount { get; init; }
    public int MissingEnterpriseCount { get; init; }
    public int MissingAcademicCount { get; init; }
    public bool CanCommit { get; init; }
    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<MentorAllocationRowPreview> Assignments { get; init; } = Array.Empty<MentorAllocationRowPreview>();
}

public sealed class MentorAllocationCommitResponse
{
    public int CreatedCount { get; init; }
    public int SkippedCount { get; init; }
}
