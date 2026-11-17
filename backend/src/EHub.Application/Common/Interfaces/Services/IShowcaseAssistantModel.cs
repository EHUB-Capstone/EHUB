namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Generates one grounded answer about the published showcase catalog. Implemented by the AI provider adapter.</summary>
public interface IShowcaseAssistantModel
{
    /// <exception cref="ShowcaseAssistantUnavailableException">The provider is disabled, overloaded or returned unusable output.</exception>
    Task<ShowcaseAssistantModelReply> AskAsync(ShowcaseAssistantModelInput input, CancellationToken cancellationToken);
}

/// <summary>Public showcase data only. Nothing here identifies a student; the team name is already shown on the showcase card.</summary>
public sealed record ShowcaseAssistantCatalogEntry(
    string Ref, string Name, string Description, string? Problem, string? Solution, string? TargetUsers, string? Highlights,
    string Stage, IReadOnlyList<string> Industries, string Semester, string Team,
    int Likes, int Views, int Comments, bool Featured, string? PublishedOn);

public sealed record ShowcaseAssistantTurn(string Role, string Content);

public sealed record ShowcaseAssistantModelInput(
    string Message, IReadOnlyList<ShowcaseAssistantTurn> History, IReadOnlyList<ShowcaseAssistantCatalogEntry> Catalog,
    int TotalPublished, DateOnly Today);

public sealed record ShowcaseAssistantPick(string Ref, string Reason);

public sealed record ShowcaseAssistantModelReply(
    string Answer, IReadOnlyList<ShowcaseAssistantPick> Picks, IReadOnlyList<string> Suggestions);

public sealed class ShowcaseAssistantUnavailableException(string userMessage) : Exception(userMessage)
{
    public string UserMessage { get; } = userMessage;
}
