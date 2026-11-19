namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Generates one grounded answer about the published showcase catalog. Implemented by the AI provider adapter.</summary>
public interface IShowcaseAssistantModel
{
    /// <param name="input">The question, recent turns and public catalog.</param>
    /// <param name="apiKey">
    /// The caller's own provider key. It is used for this call only and is passed on its own (not inside <paramref name="input"/>)
    /// so it can never end up in a logged or printed record.
    /// </param>
    /// <exception cref="ShowcaseAssistantUnavailableException">The provider is disabled, overloaded or returned unusable output.</exception>
    /// <exception cref="ShowcaseAssistantKeyRejectedException">The provider refused the key (invalid, revoked, restricted or out of quota).</exception>
    Task<ShowcaseAssistantModelReply> AskAsync(ShowcaseAssistantModelInput input, string apiKey, CancellationToken cancellationToken);
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

/// <summary>The user's own key was refused by the provider. The message is safe to show and never contains the key.</summary>
public sealed class ShowcaseAssistantKeyRejectedException(string userMessage) : Exception(userMessage)
{
    public string UserMessage { get; } = userMessage;
}
