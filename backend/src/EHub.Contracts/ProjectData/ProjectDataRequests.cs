namespace EHub.Contracts.ProjectData;

public sealed class GetProjectDataRequest
{
    public string? Search { get; init; }
    public string? SubjectCode { get; init; }

    /// <summary>Semester term: SP, SU or FA. Combines with <see cref="Year"/>; either may be omitted.</summary>
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public string? Group { get; init; }
    public string? StartupIndustry { get; init; }
    public Guid? LecturerId { get; init; }
    public Guid? MentorId { get; init; }
    public string? Achievement { get; init; }
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool IsDescending { get; init; }
}
