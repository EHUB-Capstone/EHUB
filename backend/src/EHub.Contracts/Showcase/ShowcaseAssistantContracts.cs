namespace EHub.Contracts.Showcase;

/// <summary>One earlier message of the conversation. Role is "user" or "assistant".</summary>
public sealed class ShowcaseAssistantTurnDto
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class ShowcaseAssistantChatRequest
{
    public string Message { get; init; } = string.Empty;
    /// <summary>Recent turns, oldest first, so a follow-up question keeps its context. The server keeps only the last few.</summary>
    public IReadOnlyCollection<ShowcaseAssistantTurnDto> History { get; init; } = Array.Empty<ShowcaseAssistantTurnDto>();
}

/// <summary>A published showcase the assistant recommends. Every field comes from the database, never from the model.</summary>
public sealed class ShowcaseAssistantProjectDto
{
    public Guid Id { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LogoUrl { get; init; }
    public string DevelopmentStage { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Industries { get; init; } = Array.Empty<string>();
    public int LikeCount { get; init; }
    public int ViewCount { get; init; }
    /// <summary>One sentence from the assistant explaining why this project matches the question.</summary>
    public string Reason { get; init; } = string.Empty;
}

public sealed class ShowcaseAssistantReplyDto
{
    public string Answer { get; init; } = string.Empty;
    public IReadOnlyCollection<ShowcaseAssistantProjectDto> Projects { get; init; } = Array.Empty<ShowcaseAssistantProjectDto>();
    /// <summary>Short follow-up questions the student can click.</summary>
    public IReadOnlyCollection<string> Suggestions { get; init; } = Array.Empty<string>();
}
