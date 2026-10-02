namespace EHub.Contracts.Dashboard;

public sealed class GetSubmissionAnalyticsRequest
{
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public Guid? ClassId { get; init; }
    public Guid? TeamId { get; init; }
    public int? CheckpointNumber { get; init; }
}

public sealed class SubmissionAnalyticsResponse
{
    public DateTime ServerTimeUtc { get; init; }
    public int ExpectedCount { get; init; }
    public int SubmittedCount { get; init; }
    public int NotSubmittedCount { get; init; }
    public int MissingCount { get; init; }
    public IReadOnlyCollection<SubmissionAnalyticsItemResponse> Items { get; init; } = [];
}

public sealed class SubmissionAnalyticsItemResponse
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string SemesterCode { get; init; } = string.Empty;
    public bool HasWorkspace { get; init; }
    public Guid CheckpointId { get; init; }
    public int CheckpointNumber { get; init; }
    public string CheckpointTitle { get; init; } = string.Empty;
    public decimal CourseWeight { get; init; }
    public DateTime? DeadlineUtc { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
}
