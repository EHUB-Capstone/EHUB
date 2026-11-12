namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Startup topics and their Vietnamese/English aliases, so a checklist item such as "Kết quả khảo sát"
/// also recognises "phỏng vấn 15 khách hàng". Without a catalog, only word matching is used.
/// </summary>
public sealed class CheckpointSummaryTopics(Func<string?, IReadOnlySet<string>>? findTopics)
{
    public static readonly CheckpointSummaryTopics None = new(null);

    /// <summary>Topics named in the affirmative part of the text; denied clauses are ignored.</summary>
    public IReadOnlySet<string> Find(string? text) =>
        findTopics is null || string.IsNullOrWhiteSpace(text) ? new HashSet<string>() : findTopics(text);
}
