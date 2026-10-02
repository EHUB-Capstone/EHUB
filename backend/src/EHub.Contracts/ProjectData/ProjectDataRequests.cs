namespace EHub.Contracts.ProjectData;

public sealed class GetProjectDataRequest
{
    public string? Search { get; init; }
    public Guid? SemesterId { get; init; }
    public string? SubjectCode { get; init; }
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
