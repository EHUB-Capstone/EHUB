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
    /// <summary>True when the file should be fetched through <c>download-url</c> instead of the proxy download endpoint.</summary>
    public bool CanDirectDownload { get; init; }
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
    public IReadOnlyCollection<WorkspaceCheckpointMemberScoreInput> MemberScores { get; init; } =
        Array.Empty<WorkspaceCheckpointMemberScoreInput>();
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
    public Guid? EvaluatorId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Score { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberScoreResponse>? MemberScores { get; init; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? CheckpointTotal { get; init; }
    public string? OverallFeedback { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyCollection<WorkspaceCheckpointCriterionScoreResponse> RubricScores { get; init; } =
        Array.Empty<WorkspaceCheckpointCriterionScoreResponse>();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberScoreResponse>? MemberScores { get; init; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Score { get; init; }
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
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationHistoryChangeResponse> Changes { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationHistoryChangeResponse>();
}

public sealed class WorkspaceCheckpointEvaluationHistoryChangeResponse
{
    public string Category { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? PreviousValue { get; init; }
    public string? CurrentValue { get; init; }
}

public sealed class WorkspaceCheckpointEvaluationAggregateResponse
{
    public int EvaluationCount { get; init; }
    public int SubmittedCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AverageScore { get; init; }
}

public sealed class EvaluationGradingBatchRequest
{
    public IReadOnlyCollection<Guid> TeamIds { get; init; } = Array.Empty<Guid>();
}

public sealed class EvaluationGradingBatchResponse
{
    public IReadOnlyCollection<EvaluationGradingTeamResponse> Teams { get; init; } =
        Array.Empty<EvaluationGradingTeamResponse>();
}

public sealed class EvaluationGradingTeamResponse
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public string? ProjectDescription { get; init; }
    public string? SemesterGroupName { get; init; }
    public IReadOnlyCollection<EvaluationGradingTeamMemberResponse> Members { get; init; } =
        Array.Empty<EvaluationGradingTeamMemberResponse>();
    public IReadOnlyCollection<EvaluationGradingCheckpointResponse> Checkpoints { get; init; } =
        Array.Empty<EvaluationGradingCheckpointResponse>();
    public IReadOnlyCollection<CourseAssessmentEvaluationResponse> Assessments { get; init; } =
        Array.Empty<CourseAssessmentEvaluationResponse>();
}

public sealed class EvaluationGradingTeamMemberResponse
{
    public Guid StudentId { get; init; }
    public Guid? UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RollNumber { get; init; } = string.Empty;
    public string? MajorCode { get; init; }
    public string RoleInTeam { get; init; } = string.Empty;
}

public sealed class EvaluationGradingCheckpointResponse
{
    public WorkspaceCheckpointEvaluationConfigResponse Checkpoint { get; init; } = new();
    public IReadOnlyCollection<WorkspaceCheckpointEvaluationResponse> Evaluations { get; init; } =
        Array.Empty<WorkspaceCheckpointEvaluationResponse>();
}

public sealed class WorkspaceEvaluationPublicationResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
}

public sealed class WorkspaceEvaluationUnpublicationResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public sealed class BulkWorkspaceEvaluationPublicationRequest
{
    public string Action { get; init; } = string.Empty;
    public IReadOnlyCollection<Guid> EvaluationIds { get; init; } = Array.Empty<Guid>();
}

public sealed class BulkWorkspaceEvaluationPublicationResponse
{
    public string Action { get; init; } = string.Empty;
    public string TargetStatus { get; init; } = string.Empty;
    public int RequestedCount { get; init; }
    public int ChangedCount { get; init; }
    public int UnchangedCount { get; init; }
}
