using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace EHub.Contracts.Rankings;

public sealed class GetTeamRankingsRequest
{
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public Guid? ClassId { get; init; }
    [Range(1, int.MaxValue)]
    public int? CheckpointNumber { get; init; }
}

public sealed class TeamRankingListResponse
{
    public TeamRankingSemesterResponse? ActiveSemester { get; init; }
    public TeamRankingSemesterResponse? SelectedSemester { get; init; }
    public IReadOnlyCollection<TeamRankingSemesterResponse> AvailableSemesters { get; init; } =
        Array.Empty<TeamRankingSemesterResponse>();
    public IReadOnlyCollection<TeamRankingItemResponse> Items { get; init; } =
        Array.Empty<TeamRankingItemResponse>();
}

public sealed class TeamRankingSemesterResponse
{
    public Guid Id { get; init; }
    public string Semester { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Code { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class TeamRankingItemResponse
{
    public int? Rank { get; set; }
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string TeamCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectDescription { get; init; } = string.Empty;
    public string SemesterGroupName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string Semester { get; init; } = string.Empty;
    public int Year { get; init; }
    public IReadOnlyCollection<TeamRankingCheckpointResponse> Checkpoints { get; init; } =
        Array.Empty<TeamRankingCheckpointResponse>();
    public IReadOnlyCollection<TeamRankingAssessmentResponse> Assessments { get; init; } =
        Array.Empty<TeamRankingAssessmentResponse>();
    [JsonIgnore]
    public decimal? CourseTotal { get; init; }
    public string Status { get; init; } = "INCOMPLETE";
    public int CompletedComponentCount { get; init; }
    public int PublishedComponentCount { get; init; }
    public int TotalComponentCount { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
}

public sealed class TeamRankingCheckpointResponse
{
    public Guid CheckpointId { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    [JsonIgnore]
    public decimal? Score { get; init; }
    public string Status { get; init; } = "NOT_GRADED";
}

public sealed class TeamRankingAssessmentResponse
{
    public Guid AssessmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    [JsonIgnore]
    public decimal? Score { get; init; }
    public string Status { get; init; } = "NOT_GRADED";
}
