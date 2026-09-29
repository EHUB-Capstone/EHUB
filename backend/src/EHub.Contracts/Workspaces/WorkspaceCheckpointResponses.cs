using System.Text.Json.Serialization;
using EHub.Contracts.Subjects;

namespace EHub.Contracts.Workspaces;

public sealed class WorkspaceCheckpointOverviewResponse
{
    public string SubjectCode { get; init; } = string.Empty;
    public IReadOnlyCollection<SubjectCheckpointResponse> Checkpoints { get; init; } =
        Array.Empty<SubjectCheckpointResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointSubmissionResponse> Submissions { get; init; } =
        Array.Empty<WorkspaceCheckpointSubmissionResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointFeedbackResponse> Feedbacks { get; init; } =
        Array.Empty<WorkspaceCheckpointFeedbackResponse>();
}

public sealed class WorkspaceCheckpointSubmissionResponse
{
    public int CheckpointNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? SubmittedAt { get; init; }
    public IReadOnlyCollection<WorkspaceCheckpointFileResponse> Files { get; init; } =
        Array.Empty<WorkspaceCheckpointFileResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointLinkResponse> Links { get; init; } =
        Array.Empty<WorkspaceCheckpointLinkResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointRequirementContentResponse> RequirementContents { get; init; } =
        Array.Empty<WorkspaceCheckpointRequirementContentResponse>();
}

public sealed class WorkspaceCheckpointRequirementContentResponse
{
    public int Index { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed class UpdateWorkspaceCheckpointRequirementsRequest
{
    public IReadOnlyCollection<WorkspaceCheckpointRequirementContentInput> Contents { get; init; } =
        Array.Empty<WorkspaceCheckpointRequirementContentInput>();
}

public sealed class WorkspaceCheckpointRequirementContentInput
{
    public int Index { get; init; }
    public string? Content { get; init; }
}

public sealed class WorkspaceCheckpointFileResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string OriginalName { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTime UploadedAt { get; init; }
    public WorkspaceCheckpointUserResponse? UploadedBy { get; init; }
}

public sealed class WorkspaceCheckpointLinkResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public WorkspaceCheckpointUserResponse SubmittedBy { get; init; } = new();
}

public sealed class WorkspaceCheckpointFeedbackResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public int CheckpointNumber { get; init; }
    public string Comment { get; init; } = string.Empty;
    public Guid? ParentFeedbackId { get; init; }
    public DateTime CreatedAt { get; init; }
    public WorkspaceCheckpointUserResponse? User { get; init; }
}

public sealed class WorkspaceCheckpointUserResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}

public sealed class SaveWorkspaceCheckpointEvaluationRequest
{
    public IReadOnlyCollection<WorkspaceCheckpointCriterionScoreInput> RubricScores { get; init; } =
        Array.Empty<WorkspaceCheckpointCriterionScoreInput>();
    public string? OverallFeedback { get; init; }
    public string Status { get; init; } = "DRAFT";
    public IReadOnlyCollection<WorkspaceCheckpointMemberScoreInput> MemberScoreOverrides { get; init; } =
        Array.Empty<WorkspaceCheckpointMemberScoreInput>();
}

public sealed class WorkspaceCheckpointMemberScoreInput
{
    public Guid StudentId { get; init; }
    public decimal Score { get; init; }
}

public sealed class WorkspaceCheckpointCriterionScoreInput
{
    public string CriterionKey { get; init; } = string.Empty;
    public decimal? Score { get; init; }
    public string? Comment { get; init; }
}

public sealed class WorkspaceCheckpointEvaluationSummaryResponse
{
    public WorkspaceCheckpointEvaluationConfigResponse Checkpoint { get; init; } = new();
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationResponse> Evaluations { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationHistoryResponse> History { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationHistoryResponse>();
    public WorkspaceCheckpointEvaluationAggregateResponse Summary { get; init; } = new();
}

public sealed class WorkspaceCheckpointEvaluationConfigResponse
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public decimal CourseWeight { get; init; }
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationCriterionResponse> Rubrics { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationCriterionResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberResponse> Members { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationMemberResponse>();
}

public sealed class SaveCourseAssessmentEvaluationRequest
{
    public decimal Score { get; init; }
}

public sealed class CourseAssessmentEvaluationListResponse
{
    public IReadOnlyCollection<CourseAssessmentEvaluationResponse> Assessments { get; init; } =
        Array.Empty<CourseAssessmentEvaluationResponse>();
}

public sealed class CourseAssessmentEvaluationResponse
{
    public Guid AssessmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public Guid? EvaluationId { get; init; }
    public decimal? Score { get; init; }
    public string Status { get; init; } = "NOT_GRADED";
    public DateTime? UpdatedAt { get; init; }
}

public sealed class WorkspaceCheckpointEvaluationMemberResponse
{
    public Guid StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RollNumber { get; init; } = string.Empty;
}

public sealed class WorkspaceCheckpointEvaluationCriterionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Weight { get; init; }
    public decimal MaxScore { get; init; }
    public IReadOnlyCollection<object> Levels { get; init; } = Array.Empty<object>();
}

public sealed class WorkspaceCheckpointEvaluationResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public WorkspaceCheckpointUserResponse LecturerId { get; init; } = new();
    public string EvaluatorRole { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal CheckpointTotal { get; init; }
    public string? OverallFeedback { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyCollection<WorkspaceCheckpointCriterionScoreResponse> RubricScores { get; init; } =
        Array.Empty<WorkspaceCheckpointCriterionScoreResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberScoreResponse> MemberScores { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationMemberScoreResponse>();
}

public sealed class WorkspaceCheckpointEvaluationMemberScoreResponse
{
    public Guid StudentId { get; init; }
    public decimal Score { get; init; }
    public bool IsOverridden { get; init; }
}

public sealed class WorkspaceCheckpointCriterionScoreResponse
{
    public string CriterionKey { get; init; } = string.Empty;
    public string CriterionName { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public string? Comment { get; init; }
}

public sealed class WorkspaceCheckpointEvaluationHistoryResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public int Version { get; init; }
    public WorkspaceCheckpointUserResponse ChangedBy { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public string? Note { get; init; }
}

public sealed class WorkspaceCheckpointEvaluationAggregateResponse
{
    public int EvaluationCount { get; init; }
    public int SubmittedCount { get; init; }
    public decimal AverageScore { get; init; }
}
