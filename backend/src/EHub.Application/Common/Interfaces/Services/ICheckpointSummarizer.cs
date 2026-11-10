using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Local chat model used for checkpoint summaries. Implementations never send data outside the deployment.</summary>
public interface ICheckpointSummarizer
{
    string ModelName { get; }

    /// <summary>
    /// Returns the model's JSON answer constrained by <paramref name="jsonSchema"/>. Transport errors, timeouts and
    /// malformed replies are returned as <c>WorkspaceSummaryModelUnreachable</c>, <c>WorkspaceSummaryModelTimeout</c> and
    /// <c>WorkspaceSummaryModelBadAnswer</c>; the caller validates the JSON itself.
    /// </summary>
    Task<Result<string>> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the model into memory ahead of the first request, so the first summary after a start or a long idle does not
    /// pay the load time. Best effort: failures are ignored.
    /// </summary>
    Task WarmUpAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asks the model server whether it answers and has <see cref="ModelName"/>, for the administrators' diagnostics page. It
    /// never loads the model or sends a document. The default says "all well" so a test double does not have to implement it.
    /// </summary>
    Task<SummarizerHealth> CheckHealthAsync(CancellationToken cancellationToken) => Task.FromResult(new SummarizerHealth(true, true));
}

/// <param name="Reachable">The model server answered.</param>
/// <param name="ModelAvailable">The configured model has been pulled into it (a missing model fails every request).</param>
public sealed record SummarizerHealth(bool Reachable, bool ModelAvailable);
