namespace EHub.Contracts.Showcase;

public sealed class CreateShowcaseCommentRequest
{
    public string Content { get; init; } = string.Empty;
    public Guid? ParentCommentId { get; init; }
}

public sealed class ShowcaseCommentDto
{
    public Guid Id { get; init; }
    public Guid? ParentCommentId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string? AuthorAvatarUrl { get; init; }
    public bool IsTeamReply { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>True for the author, and for administrators and the lecturers of the class (they may remove it).</summary>
    public bool CanDelete { get; init; }

    /// <summary>All replies to this comment, of which <see cref="Replies"/> holds only the oldest few.</summary>
    public int ReplyCount { get; init; }
    public IReadOnlyCollection<ShowcaseCommentDto> Replies { get; init; } = Array.Empty<ShowcaseCommentDto>();
}
