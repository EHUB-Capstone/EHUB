namespace EHub.Contracts.Users;

public sealed class LecturerImportRowPreview
{
    public int RowNumber { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Position { get; init; }
    public string? ContactEmail { get; init; }
    public string GoogleEmail { get; init; } = string.Empty;
    public string Status { get; init; } = "Ready";
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}

public sealed class LecturerImportPreviewResponse
{
    public Guid SessionId { get; init; }
    public int TotalRows { get; init; }
    public int ReadyCount { get; init; }
    public int WillActivateCount { get; init; }
    public int ExistingCount { get; init; }
    public int ErrorCount { get; init; }
    public bool CanCommit { get; init; }
    public IReadOnlyCollection<LecturerImportRowPreview> Rows { get; init; } =
        Array.Empty<LecturerImportRowPreview>();
}

public sealed class CommitLecturerImportRequest
{
    public Guid SessionId { get; init; }
}

public sealed class LecturerImportCommitResponse
{
    public int CreatedCount { get; init; }
    public int ActivatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyCollection<LecturerImportCommitError> Errors { get; init; } =
        Array.Empty<LecturerImportCommitError>();
}

public sealed class LecturerImportCommitError
{
    public int RowNumber { get; init; }
    public string GoogleEmail { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
