using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHub.Application.Common.Interfaces.Services;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Configuration;

namespace EHub.Infrastructure.Summarization;

/// <summary>
/// Calls a local Ollama chat model with a JSON schema. Settings follow the benchmark in
/// docs/workspace/AI_CHECKPOINT_SUMMARY.md: low temperature, 8k context, thinking disabled.
/// </summary>
public sealed class OllamaCheckpointSummarizer(HttpClient client, IConfiguration configuration) : ICheckpointSummarizer
{
    private readonly int _contextLength = configuration.GetValue("Summarization:ContextLength", 8192);

    public string ModelName { get; } = configuration["Summarization:Model"] ?? "qwen3.5:2b-q4_K_M";

    public async Task<Result<string>> CompleteJsonAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = ModelName,
            ["stream"] = false,
            ["think"] = false,
            // Unload soon after the queue empties so the RAM goes back to the rest of the VPS.
            ["keep_alive"] = "2m",
            ["format"] = JsonNode.Parse(jsonSchema),
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }),
            ["options"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["num_ctx"] = _contextLength,
                ["num_predict"] = 1500
            }
        };

        try
        {
            using var response = await client.PostAsJsonAsync("api/chat", body, cancellationToken);
            if (!response.IsSuccessStatusCode) return Unavailable();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
                return Unavailable();
            var text = content.GetString();
            return string.IsNullOrWhiteSpace(text) ? Unavailable() : Result.Success(text);
        }
        catch (HttpRequestException) { return Unavailable(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
    }

    private static Result<string> Unavailable() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnavailable,
        "The AI summary service is temporarily unavailable.");
}

/// <summary>Used when <c>Summarization:Provider</c> is Disabled; requests are refused before reaching it.</summary>
public sealed class DisabledCheckpointSummarizer : ICheckpointSummarizer
{
    public string ModelName => "disabled";

    public Task<Result<string>> CompleteJsonAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<string>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server."));
}
