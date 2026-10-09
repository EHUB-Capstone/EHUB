namespace EHub.Contracts.Workspaces;

public sealed class StudentPreviousScoresResponse
{
    public Guid StudentId { get; init; }
    public string? SemesterCode { get; init; }
    public IReadOnlyCollection<StudentPreviousScoreResponse> Components { get; init; } =
        Array.Empty<StudentPreviousScoreResponse>();
}

public sealed class StudentPreviousScoreResponse
{
    public Guid AssessmentId { get; init; }
    public int? CheckpointNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal Score { get; init; }
}
