namespace EHub.Contracts.Mentoring;

public sealed class MentoringSessionResponse
{
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public Guid MentorAssignmentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string? Location { get; init; }
    public string? MeetingUrl { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyCollection<MentoringActionItemResponse> ActionItems { get; init; } = [];
}

public sealed class SaveMentoringSessionRequest
{
    public Guid TeamId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string? Location { get; init; }
    public string? MeetingUrl { get; init; }
}

public sealed class SaveMentoringNotesRequest { public string Notes { get; init; } = string.Empty; }
public sealed class CreateMentoringActionItemRequest
{
    public string Content { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
}
public sealed class MentoringActionItemResponse
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public bool Completed { get; init; }
}

public sealed class SaveMentoringFeedbackRequest
{
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public sealed class MentoringFeedbackResponse
{
    public Guid Id { get; init; }
    public int Rating { get; init; }
    public string Comment { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
