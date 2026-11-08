namespace EHub.Contracts.Teams;

public sealed class TeamFeedbackAuthorDto
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public sealed class TeamFeedbackReplyDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public TeamFeedbackAuthorDto Author { get; init; } = new();
}

public sealed class TeamFeedbackThreadDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsTechnicalReview { get; init; }
    public bool Resolved { get; init; }
    public DateTime CreatedAt { get; init; }
    public int SubmissionVersion { get; init; }
    public TeamFeedbackAuthorDto Author { get; init; } = new();
    public IReadOnlyCollection<TeamFeedbackReplyDto> Replies { get; init; } = Array.Empty<TeamFeedbackReplyDto>();
}

public sealed class TeamFeedbackCriterionCommentDto
{
    public string CriterionName { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
}

/// <summary>Written evaluation feedback only. Scores are intentionally not part of this contract.</summary>
public sealed class TeamFeedbackEvaluationDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public TeamFeedbackAuthorDto Evaluator { get; init; } = new();
    public string? OverallFeedback { get; init; }
    public string? Strengths { get; init; }
    public string? Weaknesses { get; init; }
    public string? Suggestions { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public IReadOnlyCollection<TeamFeedbackCriterionCommentDto> CriterionComments { get; init; } = Array.Empty<TeamFeedbackCriterionCommentDto>();
}

public sealed class TeamFeedbackCheckpointDto
{
    public Guid CheckpointId { get; init; }
    public int CheckpointNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyCollection<TeamFeedbackThreadDto> Threads { get; init; } = Array.Empty<TeamFeedbackThreadDto>();
    public IReadOnlyCollection<TeamFeedbackEvaluationDto> Evaluations { get; init; } = Array.Empty<TeamFeedbackEvaluationDto>();
}

public sealed class TeamFeedbackOverviewDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<TeamFeedbackCheckpointDto> Checkpoints { get; init; } = Array.Empty<TeamFeedbackCheckpointDto>();
}
