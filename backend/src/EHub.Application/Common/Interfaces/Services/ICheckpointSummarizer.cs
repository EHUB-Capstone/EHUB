using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Local chat model used for checkpoint summaries. Implementations never send data outside the deployment.</summary>
public interface ICheckpointSummarizer
{
    string ModelName { get; }

    /// <summary>
    /// Returns the model's JSON answer constrained by <paramref name="jsonSchema"/>. Transport errors, timeouts and
    /// malformed replies are returned as <c>WorkspaceSummaryUnavailable</c>; the caller validates the JSON itself.
    /// </summary>
    Task<Result<string>> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken);
}
